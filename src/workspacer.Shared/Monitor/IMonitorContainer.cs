namespace workspacer
{
    public delegate void MonitorAddedDelegate(IMonitor monitor);
    public delegate void MonitorRemovedDelegate(IMonitor monitor);
    
    public interface IMonitorContainer
    {
        event MonitorAddedDelegate OnMonitorAdded;
        event MonitorRemovedDelegate OnMonitorRemoved;
        
        int NumMonitors { get; }
        IMonitor[] GetAllMonitors();
        IMonitor GetMonitorAtIndex(int index);
        IMonitor FocusedMonitor { get; set; }
        IMonitor GetMonitorAtPoint(int x, int y);
        IMonitor GetMonitorAtRect(int x, int y, int width, int height);
        IMonitor GetPreviousMonitor();
        IMonitor GetNextMonitor();
        IMonitor GetMouseMonitor();
    }
}
