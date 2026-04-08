using EnvDTE;
using ImageWatch.ViewModels;
using Microsoft.VisualStudio.Shell;
using System;

namespace ImageWatch.Debugger
{
    public class DebugSessionManager
    {
        private readonly DTE _dte;
        private readonly AsyncPackage _package;

        // Must be stored as a field to prevent COM event sink from being GC'd
        private DebuggerEvents _debuggerEvents;

        public DebugSessionManager(DTE dte, AsyncPackage package)
        {
            _dte     = dte;
            _package = package;
        }

        public void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _debuggerEvents = _dte.Events.DebuggerEvents;
            _debuggerEvents.OnEnterBreakMode  += OnEnterBreakMode;
            _debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;
            _debuggerEvents.OnEnterRunMode    += OnEnterRunMode;
        }

        private void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync();
                ImageWatchViewModel.Instance.OnBreakpointHit(_dte, _package);
            });
        }

        private void OnEnterDesignMode(dbgEventReason reason)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync();
                ImageWatchViewModel.Instance.OnDebugSessionEnded();
            });
        }

        private void OnEnterRunMode(dbgEventReason reason)
        {
            // Optionally mark variables as stale while running
        }
    }
}
