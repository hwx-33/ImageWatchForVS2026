using ImageWatch.Models;
using System;

namespace ImageWatch.Imaging
{
    public static class PixelValueFormatter
    {
        public static string[] GetPixelValues(byte[] data, MatInfo info, int row, int col)
        {
            if (data == null || info == null) return null;
            if (row < 0 || row >= info.Rows || col < 0 || col >= info.Cols) return null;

            int depth    = info.Depth;
            int channels = info.Channels;
            int bpe      = info.BytesPerElement;
            int offset   = row * info.Step + col * channels * bpe;

            if (offset + channels * bpe > data.Length) return null;

            var values = new string[channels];
            for (int c = 0; c < channels; c++)
                values[c] = FormatValue(data, offset + c * bpe, depth);
            return values;
        }

        // Formats float without trailing zeros: -0.5 → "-0.5", 8 → "8", 0.123 → "0.123"
        private static string FormatFloat(float v)
        {
            if (float.IsNaN(v)) return "NaN";
            if (float.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
            return v.ToString("0.###");
        }

        private static string FormatValue(byte[] data, int offset, int depth)
        {
            if (offset < 0 || offset >= data.Length) return "?";
            switch (depth)
            {
                case MatTypeHelper.CV_8U:  return data[offset].ToString();
                case MatTypeHelper.CV_8S:  return ((sbyte)data[offset]).ToString();
                case MatTypeHelper.CV_16U: return BitConverter.ToUInt16(data, offset).ToString();
                case MatTypeHelper.CV_16S: return BitConverter.ToInt16(data, offset).ToString();
                case MatTypeHelper.CV_32S: return BitConverter.ToInt32(data, offset).ToString();
                case MatTypeHelper.CV_32F: return FormatFloat(BitConverter.ToSingle(data, offset));
                case MatTypeHelper.CV_64F: return FormatFloat((float)BitConverter.ToDouble(data, offset));
                default: return "?";
            }
        }

        // Returns approximate 0-255 luminance for a pixel (for overlay text color selection)
        public static byte GetLuminance(byte[] data, MatInfo info, int row, int col)
        {
            if (data == null || info == null) return 128;
            int channels = info.Channels;
            int bpe      = info.BytesPerElement;
            int offset   = row * info.Step + col * channels * bpe;
            if (offset >= data.Length) return 128;

            switch (info.Depth)
            {
                case MatTypeHelper.CV_8U:
                    if (channels == 1) return data[offset];
                    double b = data[offset], g = data[offset + 1], r = channels >= 3 ? data[offset + 2] : 0;
                    return (byte)(0.114 * b + 0.587 * g + 0.299 * r);
                default:
                    return 128;
            }
        }
    }
}
