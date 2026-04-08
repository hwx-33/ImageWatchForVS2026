using ImageWatch.Models;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace ImageWatch.ViewModels
{
    public class MatVariableItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private MatInfo      _info;
        private bool         _isStale;
        private bool         _isExpanded = true;
        private BitmapSource _cachedImage;
        private BitmapSource _thumbnailImage;
        private byte[]       _cachedRawData;

        public string Name { get; }

        public MatInfo Info
        {
            get => _info;
            set
            {
                _info = value;
                Notify(nameof(Info));
                Notify(nameof(DisplayLabel));
                Notify(nameof(SizeText));
                Notify(nameof(TypeInfoText));
            }
        }

        public bool IsStale
        {
            get => _isStale;
            set { _isStale = value; Notify(nameof(IsStale)); Notify(nameof(DisplayLabel)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; Notify(nameof(IsExpanded)); }
        }

        public BitmapSource ThumbnailImage
        {
            get => _thumbnailImage;
            set { _thumbnailImage = value; Notify(nameof(ThumbnailImage)); }
        }

        public BitmapSource CachedImage
        {
            get => _cachedImage;
            set { _cachedImage = value; Notify(nameof(CachedImage)); }
        }

        public byte[] CachedRawData
        {
            get => _cachedRawData;
            set { _cachedRawData = value; Notify(nameof(CachedRawData)); }
        }

        // "640 x 480"
        public string SizeText =>
            _info != null ? $"{_info.Cols} x {_info.Rows}" : "";

        // "3 x UINT8"
        public string TypeInfoText =>
            _info != null
                ? $"{_info.Channels} x {MatTypeHelper.GetDisplayDepthName(_info.Depth)}"
                : "";

        public string DisplayLabel
        {
            get
            {
                if (_info == null) return Name;
                string stale = _isStale ? " [过期]" : "";
                return $"{Name}  {SizeText}  {TypeInfoText}{stale}";
            }
        }

        public MatVariableItem(string name) { Name = name; }

        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
