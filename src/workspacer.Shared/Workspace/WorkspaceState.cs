using System.Collections.Generic;

namespace workspacer
{
    public class WorkspaceState
    {
        public List<List<List<nint>>> MonitorWorkspaceWindows { get; set; }
        public List<int> ActiveWorkspacePerMonitor { get; set; }
        public int FocusedMonitor { get; set; }
        public nint FocusedWindow { get; set; }
    }
}
