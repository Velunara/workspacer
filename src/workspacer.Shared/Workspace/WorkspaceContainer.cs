using System;
using System.Collections.Generic;
using System.Linq;

namespace workspacer
{
    internal class WorkspacePreset
    {
        internal string Name { get; set; }
        internal Func<ILayoutEngine[]> LayoutEngine { get; set; }

        public WorkspacePreset(string name, Func<ILayoutEngine[]> layoutEngine)
        {
            Name = name;
            LayoutEngine = layoutEngine;
        }
    }
    
    public class WorkspaceContainer : IWorkspaceContainer
    {
        private static Logger _logger = Logger.Create();
        private IConfigContext _context;
        private Dictionary<IMonitor, List<IWorkspace>> _workspaces;
        private Dictionary<IWorkspace, int> _workspaceMap;
        private List<WorkspacePreset> _workspacePresets;

        private Dictionary<IMonitor, IWorkspace> _mtw;


        public WorkspaceContainer(IConfigContext context)
        {
            _context = context;

            _workspaces = new Dictionary<IMonitor, List<IWorkspace>>();
            foreach (var monitor in _context.MonitorContainer.GetAllMonitors())
            {
                _workspaces[monitor] = new List<IWorkspace>();
            }
            _mtw = new Dictionary<IMonitor, IWorkspace>();
            _workspaceMap = new Dictionary<IWorkspace, int>();

            _workspacePresets = new List<WorkspacePreset>();

            _context.MonitorContainer.OnMonitorAdded += MonitorAdded;
            _context.MonitorContainer.OnMonitorRemoved += MonitorRemoved;
        }

        private void MonitorAdded(IMonitor monitor)
        {
            _workspaces[monitor] = new List<IWorkspace>();

            foreach (var preset in _workspacePresets)
            {
                var layouts = preset.LayoutEngine();
                var newLayouts = layouts.Length > 0
                    ? _context.ProxyLayouts(layouts)
                    : _context.DefaultLayouts();

                var workspace = new Workspace(_context, preset.Name, newLayouts.ToArray())
                {
                    Monitor = monitor
                };

                _workspaces[monitor].Add(workspace);
                _workspaceMap[workspace] = _workspaces[monitor].Count - 1;
            }

            // set default active workspace
            if (_workspaces[monitor].Count > 0)
                _mtw[monitor] = _workspaces[monitor][0];

            _context.Workspaces.ForceWorkspaceUpdate();
        }

        private void MonitorRemoved(IMonitor monitor)
        {
            if (!_workspaces.ContainsKey(monitor))
                return;

            var strategy = _context.MonitorRemoveStrategy;
            var removedWorkspaces = _workspaces[monitor];

            var remainingMonitors = _workspaces.Keys
                .Where(m => m != monitor)
                .ToList();

            if (remainingMonitors.Count == 0)
                return;

            var targetMonitor = remainingMonitors.First();
            var targetWorkspaces = _workspaces[targetMonitor];

            foreach (var workspace in removedWorkspaces)
            {
                int index = _workspaceMap[workspace];

                IWorkspace destination;

                if (strategy == MonitorRemoveStrategy.Spread &&
                    index < targetWorkspaces.Count)
                {
                    destination = targetWorkspaces[index];
                }
                else
                {
                    destination = targetWorkspaces[0];
                }

                _context.Workspaces.MoveAllWindows(workspace, destination);
                _workspaceMap.Remove(workspace);
            }

            _workspaces.Remove(monitor);
            _mtw.Remove(monitor);

            _context.Workspaces.ForceWorkspaceUpdate();
        }
        
        public void SetWorkspaceForMonitor(IMonitor monitor, IWorkspace workspace)
        {
            if (_workspaces[monitor].Contains(workspace))
            {
                _mtw[monitor] = workspace;
            }
        }

        public void CreateWorkspaces(params string[] names)
        {
            foreach (var name in names)
            {
                CreateWorkspace(name, () => Array.Empty<ILayoutEngine>());
            }
        }

        public void CreateWorkspace(string name, Func<ILayoutEngine[]> layouts)
        {
            foreach (var monitor in _context.MonitorContainer.GetAllMonitors())
            {
                if (!_workspaces.ContainsKey(monitor))
                    _workspaces[monitor] = new List<IWorkspace>();

                var l = layouts();
                var newLayouts = l.Length > 0 ? _context.ProxyLayouts(l) : _context.DefaultLayouts();

                var workspace = new Workspace(_context, name, newLayouts.ToArray())
                {
                    Monitor = monitor
                };

                _workspaces[monitor].Add(workspace);
                _workspaceMap[workspace] = _workspaces[monitor].Count - 1;
            }

            _workspacePresets.Add(new WorkspacePreset(name, layouts));
            _context.Workspaces.ForceWorkspaceUpdate();
        }

        public void RemoveWorkspace(IWorkspace workspace)
        {
            var index = _workspaces[workspace.Monitor].IndexOf(workspace);
            var dest = GetPreviousWorkspace(workspace);

            _context.Workspaces.MoveAllWindows(workspace, dest);

            for (var i = index + 1; i < _workspaces[workspace.Monitor].Count; i++)
            {
                var w = _workspaces[workspace.Monitor][i];
                _workspaceMap[w]--;
            }
            _workspaces[workspace.Monitor].RemoveAt(index);

            _context.Workspaces.ForceWorkspaceUpdate();
        }
        
        public event OnWorkspaceChangeDelegate OnWorkspaceChange;
        
        public void AssignWorkspaceToMonitor(IWorkspace workspace, IMonitor monitor)
        {
            if (monitor != null && workspace != null)
            {
                workspace.IsIndicating = false;
                _mtw[monitor] = workspace;
                OnWorkspaceChange?.Invoke(workspace);
            }
        }

        public IWorkspace GetNextWorkspace(IWorkspace currentWorkspace)
        {
            VerifyExists(currentWorkspace);
            var index = _workspaceMap[currentWorkspace];
            if (index >= _workspaces[currentWorkspace.Monitor].Count - 1)
                index = 0;
            else
                index = index + 1;

            return _workspaces[currentWorkspace.Monitor][index];
        }

        public IWorkspace GetPreviousWorkspace(IWorkspace currentWorkspace)
        {
            VerifyExists(currentWorkspace);
            var index = _workspaceMap[currentWorkspace];
            if (index == 0)
                index = _workspaces[currentWorkspace.Monitor].Count - 1;
            else
                index = index - 1;

            return _workspaces[currentWorkspace.Monitor][index];
        }
        public int GetNextWorkspaceIndex(IWorkspace currentWorkspace)
        {
            VerifyExists(currentWorkspace);
            var index = GetWorkspaceIndex(currentWorkspace);
            if (index >= _workspaces[currentWorkspace.Monitor].Count - 1)
                index = 0;
            else
                index = index + 1;

            return index;
        }

        public int GetPreviousWorkspaceIndex(IWorkspace currentWorkspace)
        {
            VerifyExists(currentWorkspace);
            var index = GetWorkspaceIndex(currentWorkspace);
            if (index == 0)
                index = _workspaces[currentWorkspace.Monitor].Count - 1;
            else
                index = index - 1;

            return index;
        }


        public IWorkspace GetWorkspaceAtIndex(IWorkspace currentWorkspace, int index)
        {
            if (!VerifyExists(currentWorkspace))
            {
                return null;
            }
            if (index >= _workspaces[currentWorkspace.Monitor].Count)
                return null;

            return _workspaces[currentWorkspace.Monitor][index];
        }

        public int GetWorkspaceIndex(IWorkspace workspace)
        {
            return !VerifyExists(workspace) ? 0 : _workspaceMap[workspace];
        }

        public IMonitor GetCurrentMonitorForWorkspace(IWorkspace workspace)
        {
            return workspace.Monitor;
        }

        public IMonitor GetDesiredMonitorForWorkspace(IWorkspace workspace)
        {
            if (workspace != null)
            {
                return workspace.Monitor;
            }
            return null;
        }

        public IWorkspace GetWorkspaceForMonitor(IMonitor monitor)
        {
            if (monitor == null || !_mtw.ContainsKey(monitor))
            {
                return null;
            }
            return _mtw[monitor];
        }

        public IEnumerable<IWorkspace> GetWorkspaces(IMonitor currentMonitor)
        {
            return _workspaces[currentMonitor];
        }

        public IEnumerable<IWorkspace> GetAllWorkspaces()
        {
            return _workspaces.Values.SelectMany(x => x).ToList();
        }

        public IWorkspace GetWorkspaceOnMonitor(IMonitor monitor)
        {
            return _mtw[monitor];
        }

        public IWorkspace this[string name]
        {
            get
            {
                return _workspaces.SelectMany(x => x.Value).FirstOrDefault(w => w.Name == name);
            }
        }

        private bool VerifyExists(IWorkspace workspace)
        {
            if (workspace == null)
                return false;

            if (!_workspaceMap.ContainsKey(workspace))
                return false;

            return true;
        }
        public WorkspaceState GetState()
        {
            var monitorWorkspaceWindows = new List<List<List<nint>>>();
            var activeWorkspacePerMonitor = new List<int>();
            nint focusedWindow = 0;

            for (var i = 0; i < _context.MonitorContainer.NumMonitors; i++)
            {
                var monitor = _context.MonitorContainer.GetMonitorAtIndex(i);
                var workspacesForMonitor = _workspaces[monitor];
                var activeWorkspace = _mtw.ContainsKey(monitor) ? _mtw[monitor] : null;

                var workspaceWindowsList = new List<List<nint>>();
                for (var j = 0; j < workspacesForMonitor.Count; j++)
                {
                    var workspace = workspacesForMonitor[j];
                    var windowHandles = new List<nint>();
                    foreach (var window in workspace.Windows)
                    {
                        windowHandles.Add(window.Handle);
                        if (window.IsFocused)
                            focusedWindow = window.Handle;
                    }
                    workspaceWindowsList.Add(windowHandles);
                }

                monitorWorkspaceWindows.Add(workspaceWindowsList);
                activeWorkspacePerMonitor.Add(
                    activeWorkspace != null ? _workspaceMap[activeWorkspace] : 0);
            }

            return new WorkspaceState
            {
                MonitorWorkspaceWindows = monitorWorkspaceWindows,
                ActiveWorkspacePerMonitor = activeWorkspacePerMonitor,
                FocusedMonitor = _context.MonitorContainer.FocusedMonitor.Index,
                FocusedWindow = focusedWindow
            };
        }

        public void InitializeWithState(WorkspaceState state, IEnumerable<IWindow> allWindows)
        {
            var windows = allWindows.ToList();
            _logger.Debug("Windows: " + windows.Count + " Monitors: " + _context.MonitorContainer.NumMonitors);

            _context.MonitorContainer.FocusedMonitor =
                _context.MonitorContainer.GetMonitorAtIndex(state.FocusedMonitor);

            var monitorCount = Math.Min(_context.MonitorContainer.NumMonitors, state.MonitorWorkspaceWindows?.Count ?? 0);
            var usedHandles = new List<nint>();

            for (var i = 0; i < monitorCount; i++)
            {
                var monitor = _context.MonitorContainer.GetMonitorAtIndex(i);
                var workspacesForMonitor = _workspaces[monitor];
                var savedWorkspaceWindows = state.MonitorWorkspaceWindows[i];
                _logger.Debug("Workspaces Count: " + workspacesForMonitor.Count);
                var workspaceCount = Math.Min(workspacesForMonitor.Count, savedWorkspaceWindows.Count);

                for (var j = 0; j < workspaceCount; j++)
                {
                    var workspace = workspacesForMonitor[j];
                    foreach (var handle in savedWorkspaceWindows[j])
                    {
                        var window = windows.FirstOrDefault(w => w.Handle == handle);
                        if (window == null)
                            continue;
                        
                        _logger.Debug("Window " + window.Title + " matched");

                        var routedWorkspace = _context.WindowRouter.RouteWindow(window, workspace);
                        if (routedWorkspace == null)
                            continue;
                        
                        _logger.Debug("Window " + window.Title + " matched with route");

                        routedWorkspace.AddWindow(window);
                        _context.Workspaces.RegisterWindow(routedWorkspace, window);
                        usedHandles.Add(handle);

                        if (state.FocusedWindow == handle)
                            window.Focus();
                    }
                }

                // Restore active workspace for this monitor
                if (state.ActiveWorkspacePerMonitor != null && i < state.ActiveWorkspacePerMonitor.Count)
                {
                    var activeIdx = state.ActiveWorkspacePerMonitor[i];
                    if (activeIdx >= 0 && activeIdx < workspacesForMonitor.Count)
                        AssignWorkspaceToMonitor(workspacesForMonitor[activeIdx], monitor);
                }
            }
            
            foreach (var window in windows)
            {
                if (usedHandles.Contains(window.Handle))
                    continue;

                var defaultWorkspace = _context.Workspaces.GetWorkspaceForWindowLocation(window);
                
                var routedWorkspace = _context.WindowRouter.RouteWindow(window, defaultWorkspace);
                if (routedWorkspace == null)
                    continue;
                
                routedWorkspace.AddWindow(window);
                _context.Workspaces.RegisterWindow(routedWorkspace, window);
            }
            
            foreach (var workspaces in _workspaces.Values)
            {
                foreach (var workspace in workspaces)
                {
                    workspace.DoLayout();
                }
            }
            
            _context.Workspaces.WorkspacesUpdated();
        }
    }
}
