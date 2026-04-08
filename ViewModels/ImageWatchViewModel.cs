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

        // Singleton for access from DebugSessionManager and ToolWindow
        private static ImageWatchViewModel _instance;
        public static ImageWatchViewModel Instance =>
            _instance ?? (_instance = new ImageWatchViewModel());

        private MatVariableItem _selectedVariable;
        private BitmapSource _currentImage;
        private byte[] _currentRawData;
        private MatInfo _currentMatInfo;
        private string _statusText = "等待调试会话...";
        private bool _isLoading;

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
                if (value != null)
                    _ = LoadVariableAsync(value);
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

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        // Called from DebugSessionManager on UI thread when breakpoint is hit
        public void OnBreakpointHit(DTE dte, AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var evaluator = new MatExpressionEvaluator(dte);
            var names     = evaluator.FindMatVariableNames();

            // Build a lookup of existing variables
            var existing = new Dictionary<string, MatVariableItem>();
            foreach (var v in Variables) existing[v.Name] = v;

            // Mark all existing as stale; will un-stale those still in scope
            foreach (var v in Variables) v.IsStale = true;

            // Process found variables
            foreach (var name in names)
            {
                var info = evaluator.EvaluateMatInfo(name);
                if (info == null) continue;

                if (existing.TryGetValue(name, out var item))
                {
                    item.Info    = info;   // setter notifies DisplayLabel
                    item.IsStale = false;
                }
                else
                {
                    item = new MatVariableItem(name) { Info = info };
                    Variables.Add(item);
                }
            }

            StatusText = $"找到 {names.Count} 个 Mat 变量";

            // Auto-select first non-stale variable if nothing selected
            if ((_selectedVariable == null || _selectedVariable.IsStale) && Variables.Count > 0)
            {
                foreach (var v in Variables)
                    if (!v.IsStale) { SelectedVariable = v; break; }
            }
            else if (_selectedVariable != null && !_selectedVariable.IsStale)
            {
                // Refresh the currently selected variable
                _ = LoadVariableAsync(_selectedVariable);
            }
        }

        public void OnDebugSessionEnded()
        {
            StatusText = "调试会话已结束";
            CurrentImage    = null;
            CurrentRawData  = null;
            CurrentMatInfo  = null;
            foreach (var v in Variables) v.IsStale = true;
        }

        private async Task LoadVariableAsync(MatVariableItem item)
        {
            if (item?.Info == null) return;

            IsLoading = true;
            StatusText = $"加载 {item.Name}...";

            try
            {
                var info = item.Info;

                // Get process ID on UI thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                int pid = 0;
                try
                {
                    // Access DTE on main thread
                    var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)) as DTE;
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
                        StatusText = $"{item.Name} – {info.Cols}×{info.Rows}  {MatTypeHelper.GetTypeName(info.CvType)} [缓存]";
                    }
                    else
                    {
                        StatusText = $"{item.Name} – 无法读取数据";
                    }
                    return;
                }

                // Read memory on background thread
                byte[] rawData = await Task.Run(() =>
                    DebugMemoryReader.ReadMemory(pid, info.DataPointer, info.DataSize));

                if (rawData == null)
                {
                    StatusText = $"{item.Name} – 内存读取失败";
                    return;
                }

                // Convert to bitmap on background thread
                BitmapSource bitmap = await Task.Run(() =>
                    MatBitmapConverter.Convert(rawData, info));

                if (bitmap == null)
                {
                    StatusText = $"{item.Name} – 图像转换失败";
                    return;
                }

                // Update on UI thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                item.CachedImage   = bitmap;
                item.CachedRawData = rawData;
                CurrentImage       = bitmap;
                CurrentRawData     = rawData;
                CurrentMatInfo     = info;
                StatusText = $"{item.Name}  {info.Cols}×{info.Rows}  {MatTypeHelper.GetTypeName(info.CvType)}";
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
