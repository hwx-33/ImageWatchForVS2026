using ImageWatch.ToolWindow;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace ImageWatch.Commands
{
    internal sealed class ImageWatchWindowCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("9EEFDA4E-9D13-4F84-9F1E-4E4B4A1E8E0D");

        private readonly AsyncPackage _package;

        private ImageWatchWindowCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            _package = package;
            var id   = new CommandID(CommandSet, CommandId);
            commandService.AddCommand(new MenuCommand(Execute, id));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var svc = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (svc != null)
                new ImageWatchWindowCommand(package, svc);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await _package.ShowToolWindowAsync(
                    typeof(ImageWatchToolWindow), 0,
                    create: true,
                    cancellationToken: _package.DisposalToken);
            });
        }
    }
}
