namespace ImageWatch.Models
{
    public static class MatTypeHelper
    {
        public const int CV_8U  = 0;
        public const int CV_8S  = 1;
        public const int CV_16U = 2;
        public const int CV_16S = 3;
        public const int CV_32S = 4;
        public const int CV_32F = 5;
        public const int CV_64F = 6;

        public static int GetDepth(int cvType) => cvType & 7;
        public static int GetChannels(int cvType) => (cvType >> 3) + 1;

        public static int GetBytesPerElement(int depth)
        {
            switch (depth)
            {
                case CV_8U:  case CV_8S:  return 1;
                case CV_16U: case CV_16S: return 2;
                case CV_32S: case CV_32F: return 4;
                case CV_64F: return 8;
                default: return 1;
            }
        }

        public static string GetDepthName(int depth)
        {
            switch (depth)
            {
                case CV_8U:  return "8U";
                case CV_8S:  return "8S";
                case CV_16U: return "16U";
                case CV_16S: return "16S";
                case CV_32S: return "32S";
                case CV_32F: return "32F";
                case CV_64F: return "64F";
                default: return "?";
            }
        }

        public static string GetTypeName(int cvType)
        {
            int ch = GetChannels(cvType);
            string d = GetDepthName(GetDepth(cvType));
            return $"CV_{d}C{ch}";
        }
    }
}
