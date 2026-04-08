namespace ImageWatch.Models
{
    public class MatInfo
    {
        public string Name { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public int CvType { get; set; }
        public int Step { get; set; }
        public ulong DataPointer { get; set; }
        public bool IsValid { get; set; }

        public int Channels => MatTypeHelper.GetChannels(CvType);
        public int Depth => MatTypeHelper.GetDepth(CvType);
        public int BytesPerElement => MatTypeHelper.GetBytesPerElement(Depth);
        public int DataSize => Step * Rows;
    }
}
