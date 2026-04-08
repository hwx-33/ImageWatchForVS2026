using ImageWatch.ViewModels;
using System.Windows.Controls;

namespace ImageWatch.ToolWindow
{
    public partial class ImageWatchToolWindowControl : UserControl
    {
        public ImageWatchToolWindowControl()
        {
            InitializeComponent();
            DataContext = ImageWatchViewModel.Instance;

            // Wire up cursor and zoom callbacks from the canvas
            ImageCanvas.CursorChanged = (x, y, vals) =>
                ImageWatchViewModel.Instance.UpdateCursorInfo(x, y, vals);

            ImageCanvas.ZoomChanged = zoom =>
                ImageWatchViewModel.Instance.UpdateZoom(zoom);
        }
    }
}
