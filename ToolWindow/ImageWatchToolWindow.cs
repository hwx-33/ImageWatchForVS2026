using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;

namespace ImageWatch.ToolWindow
{
    [Guid("E56B72CA-4C65-4A81-BEF9-4F0A57A3A0E7")]
    public class ImageWatchToolWindow : ToolWindowPane
    {
        public ImageWatchToolWindow() : base(null)
        {
            Caption = "Image Watch";
            Content = new ImageWatchToolWindowControl();
        }
    }
}
