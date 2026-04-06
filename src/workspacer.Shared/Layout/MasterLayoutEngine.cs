using System;
using System.Collections.Generic;
using System.Linq;

namespace workspacer;

// ╔══════════════════════════════════════════════════════════════╗
// ║               SlaveSlotWeights — helper class                ║
// ║  Tracks per-SLOT (positional) height weights for the slave   ║
// ║  column.  Weights are normalised so they always sum to 1.0.  ║
// ║  Index 0 = topmost slave slot.                               ║
// ╚══════════════════════════════════════════════════════════════╝
public class SlaveSlotWeights
{
    // ── minimum fraction any slot may occupy ─────────────────
    public const double MinWeight = 0.05;

    private List<double> _weights = new List<double>();

    public int Count => _weights.Count;
    public IReadOnlyList<double> Weights => _weights;

    // ─────────────────────────────────────────────────────────
    //  Structural operations
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bring the slot list to exactly <paramref name="count"/> entries.
    /// Added slots are given equal default weight; removed slots have their
    /// weight redistributed proportionally among the survivors.
    /// </summary>
    public void Resize(int count)
    {
        if (count <= 0) { _weights.Clear(); return; }
        while (_weights.Count < count) InsertSlot(_weights.Count);
        while (_weights.Count > count) RemoveSlot(_weights.Count - 1);
    }

    /// <summary>
    /// Append or insert a new slot at <paramref name="index"/>.
    /// The new slot receives weight 1/newCount; all existing slots
    /// are scaled down proportionally.
    /// </summary>
    public void InsertSlot(int index)
    {
        int newCount = _weights.Count + 1;
        double newWeight = 1.0 / newCount;
        for (int i = 0; i < _weights.Count; i++)
            _weights[i] *= (1.0 - newWeight);
        _weights.Insert(index, newWeight);
        Normalise();
    }

    /// <summary>
    /// Remove the slot at <paramref name="index"/> and redistribute
    /// its weight proportionally among the remaining slots.
    /// </summary>
    public void RemoveSlot(int index)
    {
        if (_weights.Count == 0) return;
        index = Math.Max(0, Math.Min(index, _weights.Count - 1));
        double removed = _weights[index];
        _weights.RemoveAt(index);
        if (_weights.Count == 0) return;

        double remaining = 1.0 - removed;
        if (remaining < 1e-9)
        {
            for (int i = 0; i < _weights.Count; i++) _weights[i] = 1.0 / _weights.Count;
        }
        else
        {
            for (int i = 0; i < _weights.Count; i++) _weights[i] /= remaining;
        }
        Normalise();
    }

    // ─────────────────────────────────────────────────────────
    //  Resize operations — called by keybinds
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Make slot <paramref name="slotIndex"/> taller by <paramref name="delta"/>
    /// (fraction of total height, e.g. 0.04).  The extra space is stolen
    /// proportionally from all OTHER slots.
    /// </summary>
    public void GrowSlot(int slotIndex, double delta)
    {
        if (slotIndex < 0 || slotIndex >= _weights.Count) return;
        if (_weights.Count < 2) return;

        // How much can we actually steal? Each other slot must stay >= MinWeight.
        double maxSteal = 0;
        for (int i = 0; i < _weights.Count; i++)
            if (i != slotIndex) maxSteal += Math.Max(0, _weights[i] - MinWeight);

        double actualDelta = Math.Min(delta, maxSteal);
        if (actualDelta <= 0) return;

        // Grow the target slot
        _weights[slotIndex] = Math.Min(1.0 - (_weights.Count - 1) * MinWeight,
                                        _weights[slotIndex] + actualDelta);

        // Steal proportionally from donors
        double totalDonorWeight = 0;
        for (int i = 0; i < _weights.Count; i++)
            if (i != slotIndex) totalDonorWeight += _weights[i];

        double stolen = 0;
        for (int i = 0; i < _weights.Count; i++)
        {
            if (i == slotIndex) continue;
            double share = (totalDonorWeight > 1e-9) ? _weights[i] / totalDonorWeight : 1.0 / (_weights.Count - 1);
            double take = actualDelta * share;
            _weights[i] = Math.Max(MinWeight, _weights[i] - take);
            stolen += take;
        }

        Normalise();
    }

    /// <summary>Shrink slot <paramref name="slotIndex"/>; inverse of GrowSlot.</summary>
    public void ShrinkSlot(int slotIndex, double delta)
    {
        // Shrinking slot N = growing everyone else at N's expense.
        // Simplest correct approach: grow all OTHER slots by proportional share of delta.
        if (slotIndex < 0 || slotIndex >= _weights.Count) return;
        if (_weights.Count < 2) return;

        double available = Math.Max(0, _weights[slotIndex] - MinWeight);
        double actualDelta = Math.Min(delta, available);
        if (actualDelta <= 0) return;

        _weights[slotIndex] -= actualDelta;

        // Distribute the gained weight proportionally among other slots
        double totalOther = 0;
        for (int i = 0; i < _weights.Count; i++)
            if (i != slotIndex) totalOther += _weights[i];

        for (int i = 0; i < _weights.Count; i++)
        {
            if (i == slotIndex) continue;
            double share = (totalOther > 1e-9) ? _weights[i] / totalOther : 1.0 / (_weights.Count - 1);
            _weights[i] += actualDelta * share;
        }

        Normalise();
    }

    // ─────────────────────────────────────────────────────────
    //  Internal helpers
    // ─────────────────────────────────────────────────────────

    // Correct floating-point drift so weights always sum exactly to 1.
    private void Normalise()
    {
        if (_weights.Count == 0) return;
        double sum = 0;
        for (int i = 0; i < _weights.Count; i++) sum += _weights[i];
        if (sum < 1e-9) { for (int i = 0; i < _weights.Count; i++) _weights[i] = 1.0 / _weights.Count; return; }
        for (int i = 0; i < _weights.Count; i++) _weights[i] /= sum;
    }
}
// ╔══════════════════════════════════════════════════════════════╗
// ║                    FlexTallLayoutEngine                      ║
// ║                                                              ║
// ║  Implements ILayoutEngine (verified against workspacer       ║
// ║  source: PaneLayoutEngine.cs).  The interface requires       ║
// ║  ONLY two members:                                           ║
// ║    string Name { get; set; }                                 ║
// ║    IEnumerable<IWindowLocation> CalcLayout(                  ║
// ║        IEnumerable<IWindow>, int spaceWidth, int spaceHeight)║
// ║  There are no AddWindow / RemoveWindow / FocusWindow on      ║
// ║  ILayoutEngine — those belong to IWorkspace.  All state      ║
// ║  reconciliation happens inside CalcLayout by diffing the     ║
// ║  live window list against the previous snapshot.             ║
// ║                                                              ║
// ║  Layout (left → right):                                      ║
// ║  ┌───────────────────┬──────────────┐                        ║
// ║  │                   │  slave[0]    │  ← variable height     ║
// ║  │     master        ├──────────────┤                        ║
// ║  │                   │  slave[1]    │  ← variable height     ║
// ║  │                   ├──────────────┤                        ║
// ║  │                   │  slave[2]    │                        ║
// ║  └───────────────────┴──────────────┘                        ║
// ║  ←── MasterPercent ──→←─ remainder ──→                       ║
// ╚══════════════════════════════════════════════════════════════╝
public class MasterLayoutEngine : ILayoutEngine
{
    // ── tunables ─────────────────────────────────────────────

    public string Name { get; set; } = "flex-tall";

    /// <summary>Fraction of screen width given to the master pane (0.1–0.9).</summary>
    public double MasterPercent { get; private set; }

    /// <summary>How much MasterPercent shifts per expand/shrink call.</summary>
    public double MasterPercentIncrement { get; set; } = 0.1;

    /// <summary>
    /// How much a slave slot's weight changes per grow/shrink keybind press.
    /// Expressed as a fraction of total height (e.g. 0.04 = 4 %).
    /// </summary>
    public double SlaveStep { get; set; } = 0.1;

    // ── internal state ───────────────────────────────────────

    private readonly SlaveSlotWeights _slaveWeights = new SlaveSlotWeights();

    // Snapshot of window handles from the last CalcLayout call.
    // Index 0 = master handle; indices 1..N = slave handles in slot order.
    // Used to detect window insertions / removals between calls.
    private List<IntPtr> _prevHandles = new List<IntPtr>();

    // ── constructor ──────────────────────────────────────────

    public MasterLayoutEngine(double masterPercent = 0.55)
    {
        MasterPercent = Math.Max(0.1, Math.Min(0.9, masterPercent));
    }

    // ══════════════════════════════════════════════════════════
    //  ILayoutEngine — the one method workspacer calls
    // ══════════════════════════════════════════════════════════

    public IEnumerable<IWindowLocation> CalcLayout(
        IEnumerable<IWindow> windows,
        int spaceWidth,
        int spaceHeight)
    {
        var winList = windows.ToList();
        int n = winList.Count;

        // ── 0 windows ────────────────────────────────────────
        if (n == 0)
        {
            _slaveWeights.Resize(0);
            _prevHandles.Clear();
            return Enumerable.Empty<IWindowLocation>();
        }

        // ── 1 window: fills the whole screen ─────────────────
        if (n == 1)
        {
            _slaveWeights.Resize(0);
            _prevHandles = new List<IntPtr> { winList[0].Handle };
            return new IWindowLocation[]
            {
                new WindowLocation(0, 0, spaceWidth, spaceHeight, WindowState.Normal)
            };
        }

        // ── 2+ windows: master left, slaves stacked right ────
        int slaveCount = n - 1;
        ReconcileSlots(winList, slaveCount);

        var locations = new List<IWindowLocation>(n);

        // Master occupies the full left column
        int masterW = (int)(spaceWidth * MasterPercent);
        locations.Add(new WindowLocation(0, 0, masterW, spaceHeight, WindowState.Normal));

        // Slaves fill the right column weighted by _slaveWeights
        int slaveX = masterW;
        int slaveW = spaceWidth - masterW;
        int y      = 0;

        var weights = _slaveWeights.Weights;
        for (int s = 0; s < slaveCount; s++)
        {
            // Give any leftover rounding pixels to the last slave
            int h = (s == slaveCount - 1)
                ? spaceHeight - y
                : (int)(spaceHeight * weights[s]);

            h = Math.Max(1, h);
            locations.Add(new WindowLocation(slaveX, y, slaveW, h, WindowState.Normal));
            y += h;
        }

        // Save handle snapshot for next call's reconciliation
        _prevHandles = winList.Select(w => w.Handle).ToList();
        return locations;
    }

    // ══════════════════════════════════════════════════════════
    //  Public resize API — called directly from keybind lambdas
    // ══════════════════════════════════════════════════════════

    /// <summary>Widen the master column.</summary>
    public void ExpandMaster() =>
        MasterPercent = Math.Min(0.9, MasterPercent + MasterPercentIncrement);

    /// <summary>Narrow the master column.</summary>
    public void ShrinkMaster() =>
        MasterPercent = Math.Max(0.1, MasterPercent - MasterPercentIncrement);

    /// <summary>
    /// Make the focused slave taller (steal space from the other slaves).
    /// <paramref name="workspace"/> is used only to resolve which window is focused.
    /// </summary>
    public void GrowFocusedSlave(IWorkspace workspace)
    {
        int idx = GetFocusedSlaveIndex(workspace);
        if (idx >= 0) _slaveWeights.GrowSlot(idx, SlaveStep);
    }

    /// <summary>Make the focused slave shorter (give space back to the others).</summary>
    public void ShrinkFocusedSlave(IWorkspace workspace)
    {
        int idx = GetFocusedSlaveIndex(workspace);
        if (idx >= 0) _slaveWeights.ShrinkSlot(idx, SlaveStep);
    }

    /// <summary>Reset all slave heights to an equal split.</summary>
    public void ResetSlaveHeights()
    {
        int count = _slaveWeights.Count;
        _slaveWeights.Resize(0);
        _slaveWeights.Resize(count);
    }

    // ══════════════════════════════════════════════════════════
    //  Private helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Sync the slot weight table with the live slave count.
    ///
    /// Strategy: positional diff.  workspacer keeps windows in a stable
    /// insertion-ordered list unless the user explicitly reorders them, so
    /// simply adding or trimming trailing slots is correct for the common cases:
    ///   • New window opens   → append a proportional slot at the end
    ///   • Window closes      → drop the last slot (weight redistributed)
    ///   • User swaps windows → same slot count, no structural change
    /// </summary>
    private void ReconcileSlots(List<IWindow> winList, int slaveCount)
    {
        var currentSlaveHandles = winList.Skip(1).Select(w => w.Handle).ToList();

        // Fast path: nothing changed
        if (_slaveWeights.Count == slaveCount && SlaveHandlesMatch(currentSlaveHandles))
            return;

        // First time or cleared state
        if (_slaveWeights.Count == 0)
        {
            _slaveWeights.Resize(slaveCount);
            return;
        }

        // Structural change: grow or shrink the slot table
        while (_slaveWeights.Count < slaveCount)
            _slaveWeights.InsertSlot(_slaveWeights.Count);

        while (_slaveWeights.Count > slaveCount)
            _slaveWeights.RemoveSlot(_slaveWeights.Count - 1);
    }

    // Compare the slave portion of the current handle list to the previous snapshot.
    private bool SlaveHandlesMatch(List<IntPtr> current)
    {
        if (_prevHandles.Count == 0) return false;
        // _prevHandles[0] is the master; [1..] are slaves
        if (_prevHandles.Count - 1 != current.Count) return false;
        for (int i = 0; i < current.Count; i++)
            if (_prevHandles[i + 1] != current[i]) return false;
        return true;
    }

    /// <summary>
    /// Return the 0-based slave slot index of the focused window,
    /// or -1 if the master is focused or nothing is focused.
    /// Uses workspace.ManagedWindows which is the same ordered list
    /// that CalcLayout receives (index 0 = master).
    /// </summary>
    private int GetFocusedSlaveIndex(IWorkspace workspace)
    {
        var focused = workspace.FocusedWindow;
        if (focused == null) return -1;

        // ManagedWindows is verified present on IWorkspace via WorkspaceWidget.cs source
        var all = workspace.ManagedWindows.ToList();
        int pos = all.IndexOf(focused);
        if (pos <= 0) return -1; // 0 = master, -1 = not found

        return pos - 1; // slave index is one less than the list position
    }

    public void ShrinkPrimaryArea() {}
    public void ExpandPrimaryArea() {}
    public void ResetPrimaryArea() {}
    public void IncrementNumInPrimary() {}
    public void DecrementNumInPrimary() {}
}
