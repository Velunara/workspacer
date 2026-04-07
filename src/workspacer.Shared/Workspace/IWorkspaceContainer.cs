using System;
using System.Collections.Generic;

namespace workspacer
{
    public delegate void OnWorkspaceChangeDelegate(IWorkspace workspace);
    
    public interface IWorkspaceContainer
    {
        event OnWorkspaceChangeDelegate OnWorkspaceChange;
        
        void AssignWorkspaceToMonitor(IWorkspace workspace, IMonitor monitor);

        void CreateWorkspaces(params string[] names);
        void CreateWorkspace(string name, Func<ILayoutEngine[]> layouts);
        void RemoveWorkspace(IWorkspace workspace);
        void SetWorkspaceForMonitor(IMonitor monitor, IWorkspace workspace);

        IWorkspace GetNextWorkspace(IWorkspace currentWorkspace);
        IWorkspace GetPreviousWorkspace(IWorkspace currentWorkspace);
        int GetNextWorkspaceIndex(IWorkspace currentWorkspace);
        int GetPreviousWorkspaceIndex(IWorkspace currentWorkspace);

        IWorkspace GetWorkspaceAtIndex(IWorkspace currentWorkspace, int index);
        int GetWorkspaceIndex(IWorkspace workspace);

        IWorkspace GetWorkspaceForMonitor(IMonitor monitor);
        IMonitor GetCurrentMonitorForWorkspace(IWorkspace workspace);
        IMonitor GetDesiredMonitorForWorkspace(IWorkspace workspace);

        IEnumerable<IWorkspace> GetWorkspaces(IMonitor currentMonitor);
        IEnumerable<IWorkspace> GetAllWorkspaces();
        IWorkspace GetWorkspaceOnMonitor(IMonitor monitor);

        IWorkspace this[string name] { get; }
    }
}
