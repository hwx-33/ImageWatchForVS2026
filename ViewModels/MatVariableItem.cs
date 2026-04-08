using ImageWatch.Models;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace ImageWatch.ViewModels
{
    public class MatVariableItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private MatInfo _info;
        private bool _isStale;
        private BitmapSource _cachedImage;
        private byte[] _cachedRawData;

        public string Name { get; }

        public MatInfo Info
        {
            get => _info;
            set
            {
                _info = value;
                Notify(nameof(Info));
                Notify(nameof(DisplayLabel));
            }
        }

        public bool IsStale
        {
            get => _isStale;
            set
            {
                _isStale = value;
                Notify(nameof(IsStale));
                Notify(nameof(DisplayLabel));
            }
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

        public string DisplayLabel
        {
            get
            {
                if (_info == null) return Name;
                string typeStr = MatTypeHelper.GetTypeName(_info.CvType);
                string stale   = _isStale ? " [过期]" : "";
                return $"{Name}  {_info.Cols}×{_info.Rows}  {typeStr}{stale}";
            }
        }

        public MatVariableItem(string name) { Name = name; }

        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
