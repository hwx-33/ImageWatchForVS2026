using EnvDTE;
using ImageWatch.Debugger;
using ImageWatch.Imaging;
using ImageWatch.Models;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ImageWatch.ViewModels
{
    public class ImageWatchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static ImageWatchViewModel _instance;
        public static ImageWatchViewModel Instance =>
            _instance ?? (_instance = new ImageWatchViewModel());

        private MatVariableItem _selectedVariable;
        private BitmapSource    _currentImage;
        private byte[]          _currentRawData;
        private MatInfo         _currentMatInfo;
        private string          _statusText      = "等待调试会话...";
        private string          _cursorInfoText  = "";
        private string          _zoomLevelText   = "";
        private bool            _isLoading;

        public ObservableCollection<MatVariableItem> Variables { get; } =
            new ObservableCollection<MatVariableItem>();

        public MatVariableItem SelectedVariable
        {
            get => _selectedVariable;
            set
            {
                if (_selectedVariable == value) return;
                _selectedVariable = value;
                OnPropertyChanged(nameof(SelectedVariable));
                if (value != null) _ = LoadVariableAsync(value);
            }
        }

        public BitmapSource CurrentImage
        {
            get => _currentImage;
            private set { _currentImage = value; OnPropertyChanged(nameof(CurrentImage)); }
        }

        public byte[] CurrentRawData
        {
            get => _currentRawData;
            private set { _currentRawData = value; OnPropertyChanged(nameof(CurrentRawData)); }
        }

        public MatInfo CurrentMatInfo
        {
            get => _currentMatInfo;
            private set { _currentMatInfo = value; OnPropertyChanged(nameof(CurrentMatInfo)); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        // "0056 0047  |  056 100 064"
        public string CursorInfoText
        {
            get => _cursorInfoText;
            private set { _cursorInfoText = value; OnPropertyChanged(nameof(CursorInfoText)); }
        }

        // "64.00x"
        public string ZoomLevelText
        {
            get => _zoomLevelText;
            private set { _zoomLevelText = value; OnPropertyChanged(nameof(ZoomLevelText)); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        // Called from ZoomableImageCanvas on mouse move
        public void UpdateCursorInfo(int x, int y, string[] vals)
        {
            if (x < 0 || y < 0) { CursorInfoText = ""; return; }
            string coords = $"{x:D4} {y:D4}";
            if (vals != null && vals.Length > 0)
                CursorInfoText = $"{coords}  |  {string.Join("  ", vals)}";
            else
                CursorInfoText = coords;
        }

        // Called from ZoomableImageCanvas on zoom change
        public void UpdateZoom(double zoom) =>
            ZoomLevelText = $"{zoom:F2}x";

        public void OnBreakpointHit(DTE dte, AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var evaluator = new MatExpressionEvaluator(dte);
            var names     = evaluator.FindMatVariableNames();

            var existing = new Dictionary<string, MatVariableItem>();
            foreach (var v in Variables) existing[v.Name] = v;
            foreach (var v in Variables) v.IsStale = true;

            foreach (var name in names)
            {
                var info = evaluator.EvaluateMatInfo(name);
                if (info == null) continue;

                if (existing.TryGetValue(name, out var item))
                {
                    item.Info    = info;
                    item.IsStale = false;
                }
                else
                {
                    item = new MatVariableItem(name) { Info = info };
                    Variables.Add(item);
                }
            }

            StatusText = $"找到 {names.Count} 个 Mat 变量";

            if ((_selectedVariable == null || _selectedVariable.IsStale) && Variables.Count > 0)
            {
                foreach (var v in Variables)
                    if (!v.IsStale) { SelectedVariable = v; break; }
            }
            else if (_selectedVariable != null && !_selectedVariable.IsStale)
            {
                _ = LoadVariableAsync(_selectedVariable);
            }
        }

        public void OnDebugSessionEnded()
        {
            StatusText     = "调试会话已结束";
            CurrentImage   = null;
            CurrentRawData = null;
            CurrentMatInfo = null;
            CursorInfoText = "";
            ZoomLevelText  = "";
            foreach (var v in Variables) v.IsStale = true;
        }

        private async Task LoadVariableAsync(MatVariableItem item)
        {
            if (item?.Info == null) return;

            IsLoading  = true;
            StatusText = $"加载 {item.Name}...";

            try
            {
                var info = item.Info;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                int pid = 0;
                try
                {
                    var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                    if (dte?.Debugger?.CurrentProcess != null)
                        pid = dte.Debugger.CurrentProcess.ProcessID;
                }
                catch { }

                if (pid == 0 || info.DataPointer == 0 || info.DataSize <= 0)
                {
                    if (item.CachedImage != null)
                    {
                        CurrentImage   = item.CachedImage;
                        CurrentRawData = item.CachedRawData;
                        CurrentMatInfo = info;
                        StatusText     = $"{item.Name} – {info.Cols}×{info.Rows}  {MatTypeHelper.GetTypeName(info.CvType)} [缓存]";
                    }
                    else StatusText = $"{item.Name} – 无法读取数据";
                    return;
                }

                byte[] rawData = await Task.Run(() =>
                    DebugMemoryReader.ReadMemory(pid, info.DataPointer, info.DataSize));

                if (rawData == null) { StatusText = $"{item.Name} – 内存读取失败"; return; }

                BitmapSource bitmap = await Task.Run(() => MatBitmapConverter.Convert(rawData, info));

                if (bitmap == null) { StatusText = $"{item.Name} – 图像转换失败"; return; }

                // Generate thumbnail on background thread (bitmap is already frozen)
                BitmapSource thumb = await Task.Run(() =>
                    MatBitmapConverter.CreateThumbnail(bitmap));

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                item.ThumbnailImage = thumb;
                item.CachedImage    = bitmap;
                item.CachedRawData  = rawData;
                CurrentImage        = bitmap;
                CurrentRawData      = rawData;
                CurrentMatInfo      = info;
                StatusText          = $"{item.Name}  {info.Cols}×{info.Rows}  {MatTypeHelper.GetTypeName(info.CvType)}";
            }
            catch (Exception ex)
            {
                StatusText = $"错误: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
