using EnvDTE;
using ImageWatch.Commands;
using ImageWatch.Debugger;
using ImageWatch.ToolWindow;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ImageWatch
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ImageWatchPackage.PackageGuidString)]
    [ProvideToolWindow(typeof(ImageWatchToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = "34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3")] // dock alongside Output window
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class ImageWatchPackage : AsyncPackage
    {
        public const string PackageGuidString = "0c050587-b2c3-4e54-b195-fd43e98c7369";

        // Keep alive so COM events are not GC'd
        private DebugSessionManager _debugSessionManager;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Hook into the DTE debugger
            var dte = await GetServiceAsync(typeof(DTE)) as DTE;
            if (dte != null)
            {
                _debugSessionManager = new DebugSessionManager(dte, this);
                _debugSessionManager.Initialize();
            }

            // Register the menu command
            await ImageWatchWindowCommand.InitializeAsync(this);
        }
    }
}
