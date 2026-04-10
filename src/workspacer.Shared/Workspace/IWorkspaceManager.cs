namespace workspacer
{
    public delegate void WorkspaceUpdatedDelegate();
    public delegate void FocusedMonitorUpdatedDelegate();
    public delegate void WindowAddedDelegate(IWindow window, IWorkspace workspace);
    public delegate void WindowUpdatedDelegate(IWindow window, IWorkspace workspace);
    public delegate void WindowRemovedDelegate(IWindow window, IWorkspace workspace);
    public delegate void WindowMovedDelegate(IWindow window, IWorkspace oldWorkspace, IWorkspace newWorkspace);

    public interface IWorkspaceManager
    {
        // -------------------------
        // Properties
        // -------------------------

        IWorkspace FocusedWorkspace { get; }
        IWorkspace GetWorkspaceForMonitor(IMonitor monitor);

        // -------------------------
        // Workspace Switching
        // -------------------------

        void SwitchToWorkspace(int index); // uses mouse monitor
        void SwitchToWorkspace(IMonitor monitor, int index);
        void SwitchToWorkspace(IWorkspace workspace);

        void SwitchToLastFocusedWorkspace();

        void SwitchToNextWorkspace(); // mouse monitor
        void SwitchToPreviousWorkspace();

        void SwitchToNextWorkspace(IMonitor monitor);
        void SwitchToPreviousWorkspace(IMonitor monitor);

        void SwitchMonitorToWorkspace(int monitorIndex, int workspaceIndex);

        // -------------------------
        // Monitor Focus
        // -------------------------

        void SwitchFocusedMonitor(int index);
        void SwitchFocusToNextMonitor();
        void SwitchFocusToPreviousMonitor();
        void SwitchFocusedMonitorToMouseLocation();

        // -------------------------
        // Window Movement
        // -------------------------

        void MoveFocusedWindowToWorkspace(int index); // mouse monitor
        void MoveFocusedWindowToWorkspace(IMonitor monitor, int index);

        void MoveFocusedWindowToNextWorkspace();
        void MoveFocusedWindowToPreviousWorkspace();

        void MoveFocusedWindowToMonitor(int index);
        void MoveFocusedWindowToNextMonitor();
        void MoveFocusedWindowToPreviousMonitor();

        void MoveAllWindows(IWorkspace source, IWorkspace dest);
        
        // -------------------------
        // Window Lifecycle
        // -------------------------

        void AddWindow(IWindow window, bool firstCreate);
        void RemoveWindow(IWindow window);
        void UpdateWindow(IWindow window, WindowUpdateType type);

        // -------------------------
        // State / Updates
        // -------------------------

        void ForceWorkspaceUpdate();

        // -------------------------
        // Events
        // -------------------------

        event WorkspaceUpdatedDelegate WorkspaceUpdated;
        event WindowAddedDelegate WindowAdded;
        event WindowUpdatedDelegate WindowUpdated;
        event WindowRemovedDelegate WindowRemoved;
        event WindowMovedDelegate WindowMoved;
        event FocusedMonitorUpdatedDelegate FocusedMonitorUpdated;
        void RegisterWindow(IWorkspace workspace, IWindow window);
        void WorkspacesUpdated();
        IWorkspace GetWorkspaceForWindowLocation(IWindow window);
    }
}
