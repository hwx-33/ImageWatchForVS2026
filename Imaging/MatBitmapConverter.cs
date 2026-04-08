using ImageWatch.Models;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageWatch.Imaging
{
    public static class MatBitmapConverter
    {
        public static BitmapSource Convert(byte[] rawData, MatInfo info)
        {
            if (rawData == null || info == null || info.Rows <= 0 || info.Cols <= 0)
                return null;

            int width    = info.Cols;
            int height   = info.Rows;
            byte[] bgra  = new byte[width * height * 4];

            switch (info.Depth)
            {
                case MatTypeHelper.CV_8U:  Convert8U(rawData, bgra, info);  break;
                case MatTypeHelper.CV_8S:  Convert8S(rawData, bgra, info);  break;
                case MatTypeHelper.CV_16U: Convert16U(rawData, bgra, info); break;
                case MatTypeHelper.CV_16S: Convert16S(rawData, bgra, info); break;
                case MatTypeHelper.CV_32S: Convert32S(rawData, bgra, info); break;
                case MatTypeHelper.CV_32F: Convert32F(rawData, bgra, info); break;
                case MatTypeHelper.CV_64F: Convert64F(rawData, bgra, info); break;
                default: Convert8U(rawData, bgra, info); break;
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96,
                PixelFormats.Bgra32, null, bgra, width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static byte Clamp(double v) => (byte)Math.Max(0, Math.Min(255, (int)v));

        private static void Convert8U(byte[] src, byte[] dst, MatInfo info)
        {
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step;
                int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch;
                    int di = dr + c * 4;
                    if (ch == 1) { byte v = src[si]; dst[di] = v; dst[di+1] = v; dst[di+2] = v; }
                    else         { dst[di] = src[si]; dst[di+1] = src[si+1]; dst[di+2] = ch >= 3 ? src[si+2] : (byte)0; }
                    dst[di+3] = ch == 4 ? src[si+3] : (byte)255;
                }
            }
        }

        private static void Convert8S(byte[] src, byte[] dst, MatInfo info)
        {
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch; int di = dr + c * 4;
                    byte v0 = (byte)((sbyte)src[si] + 128);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = (byte)((sbyte)src[si+1]+128); dst[di+2] = ch >= 3 ? (byte)((sbyte)src[si+2]+128) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }

        private static void Convert16U(byte[] src, byte[] dst, MatInfo info)
        {
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch * 2; int di = dr + c * 4;
                    byte v0 = (byte)(BitConverter.ToUInt16(src, si) >> 8);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = (byte)(BitConverter.ToUInt16(src, si+2) >> 8); dst[di+2] = ch >= 3 ? (byte)(BitConverter.ToUInt16(src, si+4) >> 8) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }

        private static void Convert16S(byte[] src, byte[] dst, MatInfo info)
        {
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch * 2; int di = dr + c * 4;
                    byte v0 = (byte)((BitConverter.ToInt16(src, si) + 32768) >> 8);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = (byte)((BitConverter.ToInt16(src, si+2)+32768) >> 8); dst[di+2] = ch >= 3 ? (byte)((BitConverter.ToInt16(src, si+4)+32768) >> 8) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }

        private static void Convert32S(byte[] src, byte[] dst, MatInfo info)
        {
            // Find range for normalization
            int min = int.MaxValue, max = int.MinValue;
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step;
                for (int c = 0; c < info.Cols * ch; c++)
                {
                    int v = BitConverter.ToInt32(src, sr + c * 4);
                    if (v < min) min = v; if (v > max) max = v;
                }
            }
            double range = max == min ? 1.0 : max - min;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch * 4; int di = dr + c * 4;
                    byte v0 = Clamp((BitConverter.ToInt32(src, si) - min) / range * 255);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = Clamp((BitConverter.ToInt32(src, si+4) - min) / range * 255); dst[di+2] = ch >= 3 ? Clamp((BitConverter.ToInt32(src, si+8) - min) / range * 255) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }

        private static void Convert32F(byte[] src, byte[] dst, MatInfo info)
        {
            float fmin = float.MaxValue, fmax = float.MinValue;
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step;
                for (int c = 0; c < info.Cols * ch; c++)
                {
                    float v = BitConverter.ToSingle(src, sr + c * 4);
                    if (!float.IsNaN(v) && !float.IsInfinity(v))
                    { if (v < fmin) fmin = v; if (v > fmax) fmax = v; }
                }
            }
            double range = (fmax - fmin) < 1e-10 ? 1.0 : fmax - fmin;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch * 4; int di = dr + c * 4;
                    byte v0 = Clamp((BitConverter.ToSingle(src, si) - fmin) / range * 255);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = Clamp((BitConverter.ToSingle(src, si+4) - fmin) / range * 255); dst[di+2] = ch >= 3 ? Clamp((BitConverter.ToSingle(src, si+8) - fmin) / range * 255) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }

        private static void Convert64F(byte[] src, byte[] dst, MatInfo info)
        {
            double dmin = double.MaxValue, dmax = double.MinValue;
            int ch = info.Channels;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step;
                for (int c = 0; c < info.Cols * ch; c++)
                {
                    double v = BitConverter.ToDouble(src, sr + c * 8);
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    { if (v < dmin) dmin = v; if (v > dmax) dmax = v; }
                }
            }
            double range = (dmax - dmin) < 1e-10 ? 1.0 : dmax - dmin;
            for (int r = 0; r < info.Rows; r++)
            {
                int sr = r * info.Step; int dr = r * info.Cols * 4;
                for (int c = 0; c < info.Cols; c++)
                {
                    int si = sr + c * ch * 8; int di = dr + c * 4;
                    byte v0 = Clamp((BitConverter.ToDouble(src, si) - dmin) / range * 255);
                    if (ch == 1) { dst[di] = v0; dst[di+1] = v0; dst[di+2] = v0; }
                    else { dst[di] = v0; dst[di+1] = Clamp((BitConverter.ToDouble(src, si+8) - dmin) / range * 255); dst[di+2] = ch >= 3 ? Clamp((BitConverter.ToDouble(src, si+16) - dmin) / range * 255) : (byte)0; }
                    dst[di+3] = 255;
                }
            }
        }
    }
}
