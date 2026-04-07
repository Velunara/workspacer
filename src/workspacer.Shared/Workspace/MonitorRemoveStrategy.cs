namespace workspacer;

public enum MonitorRemoveStrategy
{
    /// <summary>
    /// Dump all windows on removed monitor on first best
    /// workspace
    /// </summary>
    Dump,
    
    /// <summary>
    /// Select first best monitor but keep what
    /// workspace index window was in.
    /// </summary>
    Spread
}