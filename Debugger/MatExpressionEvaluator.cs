using EnvDTE;
using ImageWatch.Models;
using System;
using System.Collections.Generic;

namespace ImageWatch.Debugger
{
    public class MatExpressionEvaluator
    {
        private readonly DTE _dte;

        public MatExpressionEvaluator(DTE dte)
        {
            _dte = dte;
        }

        /// <summary>
        /// Finds all cv::Mat local variables in the current stack frame.
        /// Must be called on the UI thread while at a breakpoint.
        /// </summary>
        public List<string> FindMatVariableNames()
        {
            var names = new List<string>();
            try
            {
                var frame = _dte.Debugger.CurrentStackFrame;
                if (frame == null) return names;

                var locals = frame.Locals;
                if (locals == null) return names;

                foreach (Expression expr in locals)
                {
                    if (expr.Type != null && expr.Type.Contains("cv::Mat"))
                        names.Add(expr.Name);
                }
            }
            catch { /* ignore - debugger may not be in break mode */ }
            return names;
        }

        /// <summary>
        /// Reads rows, cols, type, step, and data pointer for the named cv::Mat variable.
        /// Returns null if evaluation fails.
        /// </summary>
        public MatInfo EvaluateMatInfo(string variableName)
        {
            try
            {
                int rows = EvalInt($"{variableName}.rows");
                int cols = EvalInt($"{variableName}.cols");
                if (rows <= 0 || cols <= 0) return null;

                // Use flags & 4095 to get cv type (avoids calling type() method)
                int cvType = EvalInt($"{variableName}.flags & 4095");
                if (cvType < 0) cvType = 0;

                // step.buf[0] is the row stride in bytes
                int step = EvalInt($"(int)({variableName}.step.buf[0])");
                if (step <= 0)
                {
                    // Fallback: calculate from cols and element size
                    int ch  = MatTypeHelper.GetChannels(cvType);
                    int bpe = MatTypeHelper.GetBytesPerElement(MatTypeHelper.GetDepth(cvType));
                    step = cols * ch * bpe;
                }

                // Read the data pointer as an unsigned 64-bit value
                ulong dataPtr = EvalUInt64($"(unsigned long long)({variableName}.data)");
                if (dataPtr == 0) return null;

                return new MatInfo
                {
                    Name        = variableName,
                    Rows        = rows,
                    Cols        = cols,
                    CvType      = cvType,
                    Step        = step,
                    DataPointer = dataPtr,
                    IsValid     = true
                };
            }
            catch
            {
                return null;
            }
        }

        private int EvalInt(string expr)
        {
            var result = _dte.Debugger.GetExpression(expr);
            if (!result.IsValidValue) return -1;
            return int.TryParse(result.Value, out int v) ? v : -1;
        }

        private ulong EvalUInt64(string expr)
        {
            var result = _dte.Debugger.GetExpression(expr);
            if (!result.IsValidValue) return 0;

            string val = result.Value.Trim();
            // Handle hex format (e.g. "0x00007ff4abc12300")
            if (val.StartsWith("0x") || val.StartsWith("0X"))
            {
                if (ulong.TryParse(val.Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out ulong hex))
                    return hex;
            }
            return ulong.TryParse(val, out ulong dec) ? dec : 0;
        }
    }
}
