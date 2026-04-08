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
        }
    }
}
