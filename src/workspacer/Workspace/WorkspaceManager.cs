using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic.Logging;

namespace workspacer
{
    public class WorkspaceManager : IWorkspaceManager
    {
        private static Logger Logger = Logger.Create();

        private IConfigContext _context;
        private IWorkspace _lastWorkspace;
        public IWorkspace FocusedWorkspace => _context.WorkspaceContainer
            .GetWorkspaceForMonitor(_context.MonitorContainer.FocusedMonitor);

        private Dictionary<IWindow, IWorkspace> _windowsToWorkspaces;

        public event WorkspaceUpdatedDelegate WorkspaceUpdated;
        public event WindowAddedDelegate WindowAdded;
        public event WindowUpdatedDelegate WindowUpdated;
        public event WindowRemovedDelegate WindowRemoved;
        public event WindowMovedDelegate WindowMoved;
        public event FocusedMonitorUpdatedDelegate FocusedMonitorUpdated;

        public WorkspaceManager(IConfigContext context)
        {
            _context = context;
            _windowsToWorkspaces = new Dictionary<IWindow, IWorkspace>();
        }

        public void SwitchToWindow(IWindow window)
        {
            Logger.Debug("SwitchToWindow({0})", window);

            if (_windowsToWorkspaces.ContainsKey(window))
            {
                var workspace = _windowsToWorkspaces[window];
                SwitchToWorkspace(workspace);
                window.Focus();
            }
        }

        public IWorkspace GetWorkspaceForMonitor(IMonitor monitor)
        {
            return _context.WorkspaceContainer.GetWorkspaceForMonitor(monitor);
        }

        public void SwitchToWorkspace(int index)
        {
            Logger.Debug("SwitchToWorkspace({0})", index);
            var currentWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(_context.MonitorContainer.GetMouseMonitor());
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceAtIndex(currentWorkspace, index);
            SwitchToWorkspace(targetWorkspace);
        }

        public void SwitchToWorkspace(IMonitor monitor, int index)
        {
            throw new System.NotImplementedException();
        }

        public void SwitchToWorkspace(IWorkspace targetWorkspace)
        {
            Logger.Debug("SwitchToWorkspace({0})", targetWorkspace);

            if (targetWorkspace == null)
                return;

            var monitor = _context.WorkspaceContainer.GetCurrentMonitorForWorkspace(targetWorkspace);
            var currentWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(monitor);

            if (targetWorkspace != currentWorkspace)
            {
                _lastWorkspace = currentWorkspace;

                // just update active workspace for that monitor
                _context.WorkspaceContainer.SetWorkspaceForMonitor(monitor, targetWorkspace);

                currentWorkspace.DoLayout();
                targetWorkspace.DoLayout();

                WorkspaceUpdated?.Invoke();
                targetWorkspace.FocusLastFocusedWindow();
            }
        }

        public void SwitchToLastFocusedWorkspace()
        {
            Logger.Debug("SwitchToLastWorkspace({0})", _lastWorkspace);
            var targetWorkspace = _lastWorkspace;
            _lastWorkspace = FocusedWorkspace;
            SwitchToWorkspace(targetWorkspace);
        }

        public void SwitchToPreviousWorkspace(IMonitor monitor)
        {
            throw new System.NotImplementedException();
        }

        public void SwitchMonitorToWorkspace(int monitorIndex, int workspaceIndex)
        {
            if (monitorIndex >= _context.MonitorContainer.NumMonitors)
                return;

            var monitor = _context.MonitorContainer.GetMonitorAtIndex(monitorIndex);
            var currentWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(monitor);
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceAtIndex(currentWorkspace, workspaceIndex);

            if (targetWorkspace == null)
                return;

            _lastWorkspace = currentWorkspace;

            _context.WorkspaceContainer.SetWorkspaceForMonitor(monitor, targetWorkspace);

            currentWorkspace.DoLayout();
            targetWorkspace.DoLayout();

            WorkspaceUpdated?.Invoke();
            targetWorkspace.FocusLastFocusedWindow();
        }

        public void SwitchToNextWorkspace()
        {
            Logger.Debug("SwitchToNextWorkspace");
            var destMonitor = _context.MonitorContainer.FocusedMonitor;
            var currentWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(destMonitor);
            var targetWorkspace = _context.WorkspaceContainer.GetNextWorkspace(currentWorkspace);
            var sourceMonitor = _context.WorkspaceContainer.GetCurrentMonitorForWorkspace(targetWorkspace);

            _lastWorkspace = currentWorkspace;
            _context.WorkspaceContainer.AssignWorkspaceToMonitor(currentWorkspace, sourceMonitor);
            _context.WorkspaceContainer.AssignWorkspaceToMonitor(targetWorkspace, destMonitor);

            currentWorkspace.DoLayout();
            targetWorkspace.DoLayout();

            WorkspaceUpdated?.Invoke();

            targetWorkspace.FocusLastFocusedWindow();
        }

        public void SwitchToPreviousWorkspace()
        {
            Logger.Debug("SwitchToPreviousWorkspace");
            var destMonitor = _context.MonitorContainer.FocusedMonitor;
            var currentWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(destMonitor);
            var targetWorkspace = _context.WorkspaceContainer.GetPreviousWorkspace(currentWorkspace);
            var sourceMonitor = _context.WorkspaceContainer.GetCurrentMonitorForWorkspace(targetWorkspace);

            _lastWorkspace = currentWorkspace;
            _context.WorkspaceContainer.AssignWorkspaceToMonitor(currentWorkspace, sourceMonitor);
            _context.WorkspaceContainer.AssignWorkspaceToMonitor(targetWorkspace, destMonitor);

            currentWorkspace.DoLayout();
            targetWorkspace.DoLayout();

            WorkspaceUpdated?.Invoke();

            targetWorkspace.FocusLastFocusedWindow();
        }

        public void SwitchToNextWorkspace(IMonitor monitor)
        {
            throw new System.NotImplementedException();
        }

        public void SwitchFocusedMonitor(int index)
        {
            Logger.Debug("SwitchFocusedMonitor({0})", index);

            var focusedMonitor = _context.MonitorContainer.FocusedMonitor;
            if (index < _context.MonitorContainer.NumMonitors && index >= 0)
            {
                var monitor = _context.MonitorContainer.GetMonitorAtIndex(index);
                if (focusedMonitor != monitor)
                {
                    _context.MonitorContainer.FocusedMonitor = monitor;
                    FocusedWorkspace.FocusLastFocusedWindow();

                    FocusedMonitorUpdated?.Invoke();
                }
            }
        }

        public void SwitchFocusToNextMonitor()
        {
            var focusedMonitor = _context.MonitorContainer.FocusedMonitor;
            var targetMonitor = _context.MonitorContainer.GetNextMonitor();
            if (focusedMonitor != targetMonitor)
            {
                _context.MonitorContainer.FocusedMonitor = targetMonitor;
                FocusedWorkspace.FocusLastFocusedWindow();

                FocusedMonitorUpdated?.Invoke();
            }
        }

        public void SwitchFocusToPreviousMonitor()
        {
            var focusedMonitor = _context.MonitorContainer.FocusedMonitor;
            var targetMonitor = _context.MonitorContainer.GetPreviousMonitor();
            if (focusedMonitor != targetMonitor)
            {
                _context.MonitorContainer.FocusedMonitor = targetMonitor;
                FocusedWorkspace.FocusLastFocusedWindow();

                FocusedMonitorUpdated?.Invoke();
            }
        }

        public void SwitchFocusedMonitorToMouseLocation()
        {
            Logger.Debug("SwitchFocusedMonitorToMouseLocation");
            var loc = Control.MousePosition;
            var screen = Screen.FromPoint(new Point(loc.X, loc.Y));
            var monitor = _context.MonitorContainer.GetMonitorAtPoint(loc.X, loc.Y);
            _context.MonitorContainer.FocusedMonitor = monitor;
            FocusedMonitorUpdated?.Invoke();
        }

        public void MoveFocusedWindowToWorkspace(int index)
        {
            Logger.Debug("MoveFocusedWindowToWorkspace({0})", index);
            var window = FocusedWorkspace.FocusedWindow;
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceAtIndex(FocusedWorkspace, index);

            if (window != null && targetWorkspace != null)
            {
                var windows = FocusedWorkspace.ManagedWindows;
                // get next window
                var nextWindow = windows.SkipWhile(x => x != window).Skip(1).FirstOrDefault();
                if (nextWindow == null)
                {
                    // get previous window
                    nextWindow = windows.TakeWhile(x => x != window).LastOrDefault();
                }

                FocusedWorkspace.RemoveWindow(window);
                targetWorkspace.AddWindow(window);

                _windowsToWorkspaces[window] = targetWorkspace;
                WindowMoved?.Invoke(window, FocusedWorkspace, targetWorkspace);

                nextWindow?.Focus();
            }
        }

        public void MoveFocusedWindowToWorkspace(IMonitor monitor, int index)
        {
            throw new System.NotImplementedException();
        }

        public void MoveFocusedWindowToNextWorkspace()
        {
            throw new System.NotImplementedException();
        }

        public void MoveFocusedWindowToPreviousWorkspace()
        {
            throw new System.NotImplementedException();
        }

        public void MoveFocusedWindowAndSwitchToNextWorkspace()
        {
            Logger.Debug("MoveFocusedWindowAndSwitchToNextWorkspace()");
            var targetWorkspaceIndex = _context.WorkspaceContainer.GetNextWorkspaceIndex(FocusedWorkspace);
            _context.Workspaces.MoveFocusedWindowToWorkspace(targetWorkspaceIndex);
            _context.Workspaces.SwitchToNextWorkspace();
        }

        public void MoveFocusedWindowAndSwitchToPreviousWorkspace()
        {
            Logger.Debug("MoveFocusedWindowAndSwitchToPreviousWorkspace()");
            var targetWorkspaceIndex = _context.WorkspaceContainer.GetPreviousWorkspaceIndex(FocusedWorkspace);
            _context.Workspaces.MoveFocusedWindowToWorkspace(targetWorkspaceIndex);
            _context.Workspaces.SwitchToPreviousWorkspace();
        }

        public void MoveFocusedWindowToMonitor(int index)
        {
            Logger.Debug("MoveFocusedWindowToMonitor({0})", index);
            if (index >= _context.MonitorContainer.NumMonitors)
                return;

            var window = FocusedWorkspace.FocusedWindow;
            var targetMonitor = _context.MonitorContainer.GetMonitorAtIndex(index);
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(targetMonitor);

            if (window != null && targetWorkspace != null)
            {
                var windows = FocusedWorkspace.ManagedWindows;
                // get next window
                var nextWindow = windows.SkipWhile(x => x != window).Skip(1).FirstOrDefault();
                if (nextWindow == null)
                {
                    // get previous window
                    nextWindow = windows.TakeWhile(x => x != window).LastOrDefault();
                }

                FocusedWorkspace.RemoveWindow(window);
                targetWorkspace.AddWindow(window);

                _windowsToWorkspaces[window] = targetWorkspace;
                WindowMoved?.Invoke(window, FocusedWorkspace, targetWorkspace);

                nextWindow?.Focus();
            }
        }

        public void MoveFocusedWindowToNextMonitor()
        {
            Logger.Debug("MoveFocusedWindowToNextMonitor");
            var window = FocusedWorkspace.FocusedWindow;
            var focusedMonitor = _context.MonitorContainer.FocusedMonitor;
            var targetMonitor = _context.MonitorContainer.GetNextMonitor();
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(targetMonitor);

            if (window != null && targetWorkspace != null)
            {
                FocusedWorkspace.RemoveWindow(window);
                targetWorkspace.AddWindow(window);

                _windowsToWorkspaces[window] = targetWorkspace;
                WindowMoved?.Invoke(window, FocusedWorkspace, targetWorkspace);
                if (focusedMonitor != targetMonitor)
                {
                    window.Focus();
                    _context.MonitorContainer.FocusedMonitor = targetMonitor;
                    FocusedMonitorUpdated?.Invoke();
                }
            }
        }

        public void MoveFocusedWindowToPreviousMonitor()
        {
            Logger.Debug("MoveFocusedWindowToPreviousMonitor");
            var window = FocusedWorkspace.FocusedWindow;
            var focusedMonitor = _context.MonitorContainer.FocusedMonitor;
            var targetMonitor = _context.MonitorContainer.GetPreviousMonitor();
            var targetWorkspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(targetMonitor);

            if (window != null && targetWorkspace != null)
            {
                FocusedWorkspace.RemoveWindow(window);
                targetWorkspace.AddWindow(window);

                _windowsToWorkspaces[window] = targetWorkspace;
                WindowMoved?.Invoke(window, FocusedWorkspace, targetWorkspace);
                if (focusedMonitor != targetMonitor)
                {
                    _context.MonitorContainer.FocusedMonitor = targetMonitor;
                    window.Focus();
                    FocusedMonitorUpdated?.Invoke();
                }
            }
        }

        public void MoveAllWindows(IWorkspace source, IWorkspace dest)
        {
            var toMove = source.Windows.ToList();
            foreach (var window in toMove)
            {
                RemoveWindow(window);
            }
            foreach (var window in toMove)
            {
                AddWindowToWorkspace(window, dest);
            }
        }

        public void ForceWorkspaceUpdate()
        {
            WorkspaceUpdated?.Invoke();
        }

        public DateTime LastWindowAddedTime;
        public void AddWindow(IWindow window, bool firstCreate)
        {
            AddWindow(window, false, firstCreate);
        }

        private void AddWindow(IWindow window, bool switchToWorkspace, bool firstCreate)
        {
            Logger.Debug("AddWindow({0})", window);

            if (!_windowsToWorkspaces.ContainsKey(window))
            {
                IWorkspace workspace;
                if (_context.Initializing)
                {
                    workspace = firstCreate ? _context.WindowRouter.RouteWindow(window) : GetWorkspaceForWindowLocation(window);
                    LastWindowAddedTime = DateTime.Now;
                }
                else
                {
                    var mouseMonitor = _context.MonitorContainer.GetMouseMonitor();
                    workspace = firstCreate ? _context.WindowRouter.RouteWindow(window) : GetWorkspaceForMonitor(mouseMonitor);
                }

                if (workspace != null)
                {
                    AddWindowToWorkspace(window, workspace);

                    if (switchToWorkspace && window.CanLayout)
                    {
                        SwitchToWorkspace(workspace);
                    }
                }
            }
        }

        public void RegisterWindow(IWorkspace workspace, IWindow window)
        {
            _windowsToWorkspaces[window] = workspace;
        }

        private void AddWindowToWorkspace(IWindow window, IWorkspace workspace)
        {
            Logger.Debug("AddWindowToWorkspace({0}, {1})", window, workspace);
            workspace.AddWindow(window);
            _windowsToWorkspaces[window] = workspace;

            if (window.IsFocused)
            {
                var monitor = _context.WorkspaceContainer.GetCurrentMonitorForWorkspace(workspace);
                if (monitor != null)
                {
                    _context.MonitorContainer.FocusedMonitor = monitor;
                }
            }
            WindowAdded?.Invoke(window, workspace);
        }

        public void RemoveWindow(IWindow window)
        {
            if (_windowsToWorkspaces.ContainsKey(window))
            {
                Logger.Debug("RemoveWindow({0})", window);
                var workspace = _windowsToWorkspaces[window];
                _windowsToWorkspaces[window].RemoveWindow(window);
                _windowsToWorkspaces.Remove(window);
                WindowRemoved?.Invoke(window, workspace);
            }
        }

        private int _moves = 0;
        public void UpdateWindow(IWindow window, WindowUpdateType type)
        {
            if (type is WindowUpdateType.Move && (window.Location.Width != window.TilePosition.Width ||
                                                  window.Location.Height != window.TilePosition.Height) && _moves < 2)
            {
                type = WindowUpdateType.Scale;
            }
            
            if (window.CodeMoved && type is WindowUpdateType.Move or WindowUpdateType.MoveStart or WindowUpdateType.MoveEnd or WindowUpdateType.Scale or WindowUpdateType.ScaleEnd)
            {
                window.CodeMoved = false;
                return;
            }
            
            if (_windowsToWorkspaces.ContainsKey(window))
            {
                Logger.Trace("UpdateWindow({0})", window);
                var workspace = _windowsToWorkspaces[window];
                if (window.IsFocused)
                {
                    var monitor = _context.WorkspaceContainer.GetCurrentMonitorForWorkspace(workspace);
                    if (monitor != null)
                    {
                        _context.MonitorContainer.FocusedMonitor = monitor;
                    }
                    else
                    {
                        if (type == WindowUpdateType.Foreground)
                        {
                            // TODO: show flash for workspace (in bar?)
                            Logger.Trace("workspace.IsIndicating = true for workspace {0}", workspace);
                            // workspace.IsIndicating = true;
                            WorkspaceUpdated?.Invoke();
                        }
                    }

                    // Only change the current window if we are actively moving it.
                    if (type == WindowUpdateType.Move && window.IsMouseMoving)
                    {
                        _moves += 1;
                        if (window.MoveInitiated)
                        {
                            TrySwapWindowToMouse(window);
                        }
                        else
                        {
                            window.MoveInitiated = true;
                        }
                    }
                    else
                    {
                        window.MoveInitiated = false;
                        _moves = 0;
                    }
                    _windowsToWorkspaces[window].UpdateWindow(window, type);
                    WindowUpdated?.Invoke(window, workspace);
                }
            }
        }

        public void HandleWindowUpdated(IWindow window)
        {
            if (_windowsToWorkspaces.ContainsKey(window))
            {
                Logger.Trace("UpdateWindow({0})", window);
                var workspace = _windowsToWorkspaces[window];
                WindowUpdated?.Invoke(window, workspace);
            }
        }

        private void TrySwapWindowToMouse(IWindow window)
        {
            var point = Control.MousePosition;
            int x = point.X;
            int y = point.Y;

            var currentWorkspace = _windowsToWorkspaces[window];

            if (currentWorkspace.IsPointInside(x, y))
            {
                currentWorkspace.SwapWindowToPoint(window, x, y);
            } else
            {
                foreach (var monitor in _context.MonitorContainer.GetAllMonitors())
                {
                    var workspace = _context.WorkspaceContainer.GetWorkspaceForMonitor(monitor);
                    if (workspace.IsPointInside(x, y))
                    {
                        currentWorkspace.RemoveWindow(window, false);
                        workspace.AddWindow(window, false);
                        _windowsToWorkspaces[window] = workspace;

                        workspace.SwapWindowToPoint(window, x, y);
                        currentWorkspace.DoLayout();
                    }
                }
            }
        }

        public IWorkspace GetWorkspaceForWindowLocation(IWindow window)
        {
            var location = window.Location;
            var monitor = _context.MonitorContainer.GetMonitorAtRect(location.X, location.Y, location.Width, location.Height);
            return _context.WorkspaceContainer.GetWorkspaceForMonitor(monitor);
        }

        public WorkspaceState GetState()
        {
            return _context.WorkspaceContainer.GetState();
        }

        public void InitializeWithState(WorkspaceState state, IEnumerable<IWindow> allWindows)
        {
            _context.WorkspaceContainer.InitializeWithState(state, allWindows);
        }

        public void Initialize(IEnumerable<IWindow> windows)
        {
            for (var i = 0; i < _context.MonitorContainer.NumMonitors; i++)
            {
                var m = _context.MonitorContainer.GetMonitorAtIndex(i);
                var w = _context.WorkspaceContainer.GetWorkspaces(m).First();
                _context.WorkspaceContainer.AssignWorkspaceToMonitor(w, m);
            }

            foreach (var w in windows)
            {
                var locationWorkspace = GetWorkspaceForWindowLocation(w);
                var destWorkspace = _context.WindowRouter.RouteWindow(w, locationWorkspace);

                if (destWorkspace != null)
                {
                    AddWindowToWorkspace(w, destWorkspace);
                }
            }
        }

        public void WorkspacesUpdated()
        {
            WorkspaceUpdated?.Invoke();
        }
    }
}