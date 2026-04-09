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
    public const double MinWeight = 0.05;

    private List<double> _weights = new List<double>();

    public int Count => _weights.Count;
    public IReadOnlyList<double> Weights => _weights;

    // ── Structural operations ─────────────────────────────────

    public void Resize(int count)
    {
        if (count <= 0) { _weights.Clear(); return; }
        while (_weights.Count < count) InsertSlot(_weights.Count);
        while (_weights.Count > count) RemoveSlot(_weights.Count - 1);
    }

    public void InsertSlot(int index)
    {
        int newCount = _weights.Count + 1;
        double newWeight = 1.0 / newCount;
        for (int i = 0; i < _weights.Count; i++)
            _weights[i] *= (1.0 - newWeight);
        _weights.Insert(index, newWeight);
        Normalise();
    }

    public void RemoveSlot(int index)
    {
        if (_weights.Count == 0) return;
        index = Math.Max(0, Math.Min(index, _weights.Count - 1));
        double removed = _weights[index];
        _weights.RemoveAt(index);
        if (_weights.Count == 0) return;

        double remaining = 1.0 - removed;
        if (remaining < 1e-9)
            for (int i = 0; i < _weights.Count; i++) _weights[i] = 1.0 / _weights.Count;
        else
            for (int i = 0; i < _weights.Count; i++) _weights[i] /= remaining;
        Normalise();
    }

    // ── Keybind-driven resize ─────────────────────────────────

    public void GrowSlot(int slotIndex, double delta)
    {
        if (slotIndex < 0 || slotIndex >= _weights.Count) return;
        if (_weights.Count < 2) return;

        double maxSteal = 0;
        for (int i = 0; i < _weights.Count; i++)
            if (i != slotIndex) maxSteal += Math.Max(0, _weights[i] - MinWeight);

        double actualDelta = Math.Min(delta, maxSteal);
        if (actualDelta <= 0) return;

        _weights[slotIndex] = Math.Min(1.0 - (_weights.Count - 1) * MinWeight,
                                        _weights[slotIndex] + actualDelta);

        double totalDonorWeight = 0;
        for (int i = 0; i < _weights.Count; i++)
            if (i != slotIndex) totalDonorWeight += _weights[i];

        for (int i = 0; i < _weights.Count; i++)
        {
            if (i == slotIndex) continue;
            double share = (totalDonorWeight > 1e-9) ? _weights[i] / totalDonorWeight : 1.0 / (_weights.Count - 1);
            _weights[i] = Math.Max(MinWeight, _weights[i] - actualDelta * share);
        }
        Normalise();
    }

    public void ShrinkSlot(int slotIndex, double delta)
    {
        if (slotIndex < 0 || slotIndex >= _weights.Count) return;
        if (_weights.Count < 2) return;

        double available = Math.Max(0, _weights[slotIndex] - MinWeight);
        double actualDelta = Math.Min(delta, available);
        if (actualDelta <= 0) return;

        _weights[slotIndex] -= actualDelta;

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

    // ── Divider drag ──────────────────────────────────────────

    /// <summary>
    /// Move the divider between <paramref name="upperSlot"/> and the slot below it.
    /// <paramref name="deltaFraction"/> is signed: positive = divider moves down
    /// (upper side grows, lower side shrinks).
    ///
    /// <paramref name="slaveIdx"/> must equal either <paramref name="upperSlot"/> or
    /// <c>upperSlot + 1</c> and controls which side is the "active" one:
    ///
    ///   slaveIdx == upperSlot  →  that single slot absorbs the full delta;
    ///                             the entire lower group [lowerSlot..end] scales
    ///                             proportionally; all other upper slots are fixed.
    ///
    ///   slaveIdx == lowerSlot  →  that single slot absorbs the full delta;
    ///                             the entire upper group [0..upperSlot] scales
    ///                             proportionally; all other lower slots are fixed.
    /// </summary>
    public void MoveDivider(int upperSlot, double deltaFraction, int slaveIdx)
    {
        int lowerSlot = upperSlot + 1;
        if (upperSlot < 0 || lowerSlot >= _weights.Count) return;

        bool slaveIsUpper = (slaveIdx == upperSlot);

        if (slaveIsUpper)
        {
            // ── Solo slot: _weights[upperSlot]  ───────────────────────────────
            // ── Scaled group: [lowerSlot .. end]  ────────────────────────────

            double sumLower = 0;
            int    lowerCount = _weights.Count - lowerSlot;
            for (int i = lowerSlot; i < _weights.Count; i++) sumLower += _weights[i];

            // Upper slot cannot drop below MinWeight.
            // Lower group cannot lose so much that any slot drops below MinWeight
            // (conservative bound: treat as if the group were uniform).
            double minDelta = MinWeight - _weights[upperSlot];
            double maxDelta = sumLower - lowerCount * MinWeight;
            double clamped  = Math.Max(minDelta, Math.Min(maxDelta, deltaFraction));
            if (Math.Abs(clamped) < 1e-12) return;

            _weights[upperSlot] += clamped;

            double newSumLower = sumLower - clamped;          // zero-sum: upper gains = lower loses
            double scale       = sumLower > 1e-12
                ? newSumLower / sumLower
                : 1.0 / lowerCount;

            for (int i = lowerSlot; i < _weights.Count; i++)
                _weights[i] *= scale;
        }
        else // slaveIsLower
        {
            // ── Solo slot: _weights[lowerSlot]  ───────────────────────────────
            // ── Scaled group: [0 .. upperSlot]  ──────────────────────────────

            double sumUpper = 0;
            int    upperCount = upperSlot + 1;
            for (int i = 0; i <= upperSlot; i++) sumUpper += _weights[i];

            // Lower slot cannot drop below MinWeight.
            // Upper group cannot lose so much that any slot drops below MinWeight.
            double maxDelta = _weights[lowerSlot] - MinWeight;
            double minDelta = upperCount * MinWeight - sumUpper;
            double clamped  = Math.Max(minDelta, Math.Min(maxDelta, deltaFraction));
            if (Math.Abs(clamped) < 1e-12) return;

            _weights[lowerSlot] -= clamped;                   // lower absorbs the mirror amount

            double newSumUpper = sumUpper + clamped;
            double scale       = sumUpper > 1e-12
                ? newSumUpper / sumUpper
                : 1.0 / upperCount;

            for (int i = 0; i <= upperSlot; i++)
                _weights[i] *= scale;
        }

        // Both branches are zero-sum by construction — global sum stays 1.0.
        // No Normalise() call needed.
    }

    // ── Internal ──────────────────────────────────────────────

    private void Normalise()
    {
        if (_weights.Count == 0) return;
        double sum = _weights.Sum();
        if (sum < 1e-9) { for (int i = 0; i < _weights.Count; i++) _weights[i] = 1.0 / _weights.Count; return; }
        for (int i = 0; i < _weights.Count; i++) _weights[i] /= sum;
    }
}

// ╔══════════════════════════════════════════════════════════════╗
// ║             WindowLocation extension helpers                 ║
// ║                                                              ║
// ║  Used by CalcLayout to detect which edges of a window have   ║
// ║  moved relative to its last tiled position (TilePosition).   ║
// ╚══════════════════════════════════════════════════════════════╝
public static class WindowLocationExtensions
{
    public static int Right (this IWindowLocation l) => l.X + l.Width;
    public static int Bottom(this IWindowLocation l) => l.Y + l.Height;
}

// ╔══════════════════════════════════════════════════════════════╗
// ║                    MasterLayoutEngine                        ║
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
// ║                                                              ║
// ║  Corner-drag resize is detected entirely inside CalcLayout   ║
// ║  by comparing each window's Location (where it is now,       ║
// ║  i.e. after a potential corner drag) against its             ║
// ║  TilePosition (where CalcLayout last placed it).             ║
// ║                                                              ║
// ║  Resizable edges                                             ║
// ║  ────────────────                                            ║
// ║  master RIGHT  edge → MasterPercent  (+ = wider master)      ║
// ║  slave  LEFT   edge → MasterPercent  (+ = wider master)      ║
// ║  slave  BOTTOM edge → divider(s, s+1) (+ = taller slave[s])  ║
// ║  slave  TOP    edge → divider(s-1, s) (+ = taller slave[s-1])║
// ║                                                              ║
// ║  Screen edges (master left/top/bottom, slave right,          ║
// ║  first slave top, last slave bottom) are never resizable.    ║
// ╚══════════════════════════════════════════════════════════════╝
public class MasterLayoutEngine : ILayoutEngine
{
    // ── Tunables ──────────────────────────────────────────────
    private Logger Logger = Logger.Create();

    public string Name { get; set; } = "flex-tall";

    /// <summary>Fraction of screen width given to the master pane (0.1–0.9).</summary>
    public double MasterPercent { get; private set; }

    /// <summary>How much MasterPercent shifts per keybind expand/shrink call.</summary>
    public double MasterPercentIncrement { get; set; } = 0.1;

    /// <summary>How much a slave slot's weight changes per keybind grow/shrink call.</summary>
    public double SlaveStep { get; set; } = 0.1;

    // ── Internal state ────────────────────────────────────────

    private readonly SlaveSlotWeights _slaveWeights = new SlaveSlotWeights();

    // Handle snapshot from the previous CalcLayout call.
    // Index 0 = master; 1..N = slave slots in order.
    private List<IntPtr> _prevHandles = new List<IntPtr>();

    // ── Constructor ───────────────────────────────────────────

    public MasterLayoutEngine(double masterPercent = 0.55)
    {
        MasterPercent = Math.Max(0.1, Math.Min(0.9, masterPercent));
    }

    // ══════════════════════════════════════════════════════════
    //  CalcLayout — the single ILayoutEngine method
    // ══════════════════════════════════════════════════════════

    public IEnumerable<IWindowLocation> CalcLayout(
        IEnumerable<IWindow> windows,
        int spaceWidth,
        int spaceHeight)
    {
        var winList = windows.ToList();
        int n = winList.Count;

        if (n == 0)
        {
            _slaveWeights.Resize(0);
            _prevHandles.Clear();
            return Enumerable.Empty<IWindowLocation>();
        }

        if (n == 1)
        {
            _slaveWeights.Resize(0);
            _prevHandles = new List<IntPtr> { winList[0].Handle };
            return new[] { new WindowLocation(0, 0, spaceWidth, spaceHeight, WindowState.Normal, LocationLockAxis.All) };
        }

        // ── 2+ windows ────────────────────────────────────────

        int slaveCount = n - 1;
        ReconcileSlots(winList, slaveCount);

        // Detect corner-drag resizes BEFORE recomputing positions.
        // If any window's current Location differs from its TilePosition on a
        // resizable edge, fold that delta into MasterPercent / slave weights.
        ApplyDragResizes(winList, spaceWidth, spaceHeight);

        // ── Compute new tile positions ─────────────────────────

        var locations = new List<IWindowLocation>(n);

        int masterW = (int)(spaceWidth * MasterPercent);
        locations.Add(new WindowLocation(0, 0, masterW, spaceHeight, WindowState.Normal, LocationLockAxis.AllExceptRight));

        int slaveX = masterW;
        int slaveW = spaceWidth - masterW;
        int y = 0;
        var weights = _slaveWeights.Weights;

        for (int s = 0; s < slaveCount; s++)
        {
            int h = (s == slaveCount - 1)
                ? spaceHeight - y
                : (int)(spaceHeight * weights[s]);

            h = Math.Max(1, h);
            LocationLockAxis locationLock = LocationLockAxis.Right;
            if (s == 0)
            {
                locationLock |= LocationLockAxis.Top;
            }
            
            if (s == slaveCount - 1)
            {
                locationLock |= LocationLockAxis.Bottom;
            }
            
            locations.Add(new WindowLocation(slaveX, y, slaveW, h, WindowState.Normal, locationLock));
            y += h;
        }

        _prevHandles = winList.Select(w => w.Handle).ToList();
        return locations;
    }

    // ══════════════════════════════════════════════════════════
    //  Drag-resize detection
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// For each window, compare Location (current OS position, reflecting any
    /// corner drag the user just performed) with TilePosition (the position
    /// CalcLayout assigned last frame).  Differences on resizable edges are
    /// converted to fraction deltas and applied to MasterPercent or the
    /// appropriate slave-slot divider.
    ///
    /// Why each edge maps the way it does:
    ///
    ///   Master RIGHT edge
    ///     This is exactly the vertical column divider.  Moving it right by Δx
    ///     pixels widens the master column by Δx/spaceWidth.
    ///
    ///   Slave LEFT edge
    ///     Also the vertical column divider, seen from the slave side.  The
    ///     slave column narrows when the divider moves right, but MasterPercent
    ///     is measured from the left, so the sign is the same: +Δx → wider master.
    ///
    ///   Slave BOTTOM edge  (not the last slave)
    ///     This is the horizontal divider between slave[s] and slave[s+1].
    ///     Moving it down by Δy pixels makes slave[s] taller by Δy/spaceHeight.
    ///     → MoveDivider(s, +fraction)
    ///
    ///   Slave TOP edge  (not the first slave)
    ///     This is the horizontal divider between slave[s-1] and slave[s],
    ///     seen from slave[s]'s perspective.  Moving this edge DOWN shrinks
    ///     slave[s] from the top, which means the divider moved down and
    ///     slave[s-1] grew.
    ///     → MoveDivider(s-1, +fraction)
    ///
    ///   Screen-bound edges (master left/top/bottom, slave right, first slave
    ///   top, last slave bottom) are ignored — they have nowhere to go.
    ///
    /// Corner drags naturally move two edges at once (e.g. bottom-right moves
    /// the right edge and the bottom edge).  Each axis is independent, so both
    /// get applied without conflict.
    /// </summary>
    private void ApplyDragResizes(List<IWindow> winList, int spaceWidth, int spaceHeight)
    {
        Logger.Debug("---- ApplyDragResizes ----");

        for (int i = 0; i < winList.Count; i++)
        {
            var win = winList[i];
            var tiled = win.TilePosition;
            var loc = win.Location;

            if (tiled == null || loc == null)
                continue;

            if (loc.Width <= 0 || loc.Height <= 0)
            {
                Logger.Debug($"  SKIP invalid size: w={loc.Width}, h={loc.Height}");
                continue;
            }

            bool isMaster = (i == 0);
            int slaveIdx = i - 1;

            int xDelta = loc.X - tiled.X;
            int yDelta = loc.Y - tiled.Y;
            int wDelta = loc.Width - tiled.Width;
            int hDelta = loc.Height - tiled.Height;
            int rightDelta = loc.Right() - tiled.Right();
            int bottomDelta = loc.Bottom() - tiled.Bottom();

            bool horizontalResize = wDelta != 0;
            bool verticalResize = hDelta != 0;

            Logger.Debug(
                $"Win[{i}] " +
                $"isMaster={isMaster} " +
                $"xΔ={xDelta} yΔ={yDelta} wΔ={wDelta} hΔ={hDelta} " +
                $"rightΔ={rightDelta} bottomΔ={bottomDelta}"
            );

            bool handledAnyResize = false;

            if (isMaster)
            {
                // Master: right edge resize only. Left edge stays anchored.
                if (horizontalResize && xDelta == 0)
                {
                    double newPercent = (double)loc.Width / spaceWidth;
                    Logger.Debug($"  MASTER resize → width={loc.Width}, percent={newPercent:0.000}");
                    MasterPercent = Math.Max(0.1, Math.Min(0.9, newPercent));
                    Logger.Debug($"  MASTER clamped percent={MasterPercent:0.000}");
                    handledAnyResize = true;
                }
            }
            else
            {
                // Slave: left edge resize only. Right edge stays anchored.
                if (horizontalResize && rightDelta == 0)
                {
                    double newPercent = 1 - (double)loc.Width / spaceWidth;
                    Logger.Debug($"  SLAVE resize → x={loc.X}, percent={newPercent:0.000}");
                    MasterPercent = Math.Max(0.1, Math.Min(0.9, newPercent));
                    Logger.Debug($"  SLAVE clamped percent={MasterPercent:0.000}");
                    handledAnyResize = true;
                }

                // Bottom edge resize.
                if (verticalResize && slaveIdx < _slaveWeights.Count - 1 && yDelta == 0)
                {
                    double frac = (double)bottomDelta / spaceHeight;
                    Logger.Debug(
                        $"  DIVIDER[{slaveIdx}] BOTTOM move: " +
                        $"dy={bottomDelta}, frac={frac:0.000}"
                    );
                    _slaveWeights.MoveDivider(slaveIdx, frac, slaveIdx);
                    Logger.Debug(
                        $"  DIVIDER[{slaveIdx}] AFTER: " +
                        $"{string.Join(", ", _slaveWeights.Weights.Select(w => w.ToString("0.000")))}"
                    );
                    handledAnyResize = true;
                }
                // Top edge resize.
                else if (verticalResize && slaveIdx > 0 && bottomDelta == 0)
                {
                    int divider = slaveIdx - 1;
                    double frac = (double)yDelta / spaceHeight;
                    Logger.Debug(
                        $"  DIVIDER[{divider}] TOP move: " +
                        $"dy={yDelta}, frac={frac:0.000}"
                    );
                    _slaveWeights.MoveDivider(divider, frac, slaveIdx);
                    Logger.Debug(
                        $"  DIVIDER[{divider}] AFTER: " +
                        $"{string.Join(", ", _slaveWeights.Weights.Select(w => w.ToString("0.000")))}"
                    );
                    handledAnyResize = true;
                }
            }

            // Important: stop after the first real resize source.
            // Any later window deltas are usually the result of relayout, not user drag.
            if (handledAnyResize)
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Public keybind API
    // ══════════════════════════════════════════════════════════

    /// <summary>Widen the master column.</summary>
    public void ExpandMaster() =>
        MasterPercent = Math.Min(0.9, MasterPercent + MasterPercentIncrement);

    /// <summary>Narrow the master column.</summary>
    public void ShrinkMaster() =>
        MasterPercent = Math.Max(0.1, MasterPercent - MasterPercentIncrement);

    /// <summary>Make the focused slave taller.</summary>
    public void GrowFocusedSlave(IWorkspace workspace)
    {
        int idx = GetFocusedSlaveIndex(workspace);
        if (idx >= 0) _slaveWeights.GrowSlot(idx, SlaveStep);
    }

    /// <summary>Make the focused slave shorter.</summary>
    public void ShrinkFocusedSlave(IWorkspace workspace)
    {
        int idx = GetFocusedSlaveIndex(workspace);
        if (idx >= 0) _slaveWeights.ShrinkSlot(idx, SlaveStep);
    }

    /// <summary>Reset all slave heights to equal shares.</summary>
    public void ResetSlaveHeights()
    {
        int count = _slaveWeights.Count;
        _slaveWeights.Resize(0);
        _slaveWeights.Resize(count);
    }

    // ══════════════════════════════════════════════════════════
    //  Private helpers
    // ══════════════════════════════════════════════════════════

    private void ReconcileSlots(List<IWindow> winList, int slaveCount)
    {
        var currentSlaveHandles = winList.Skip(1).Select(w => w.Handle).ToList();

        if (_slaveWeights.Count == slaveCount && SlaveHandlesMatch(currentSlaveHandles))
            return;

        if (_slaveWeights.Count == 0)
        {
            _slaveWeights.Resize(slaveCount);
            return;
        }

        while (_slaveWeights.Count < slaveCount) _slaveWeights.InsertSlot(_slaveWeights.Count);
        while (_slaveWeights.Count > slaveCount) _slaveWeights.RemoveSlot(_slaveWeights.Count - 1);
    }

    private bool SlaveHandlesMatch(List<IntPtr> current)
    {
        if (_prevHandles.Count == 0) return false;
        if (_prevHandles.Count - 1 != current.Count) return false;
        for (int i = 0; i < current.Count; i++)
            if (_prevHandles[i + 1] != current[i]) return false;
        return true;
    }

    private int GetFocusedSlaveIndex(IWorkspace workspace)
    {
        var focused = workspace.FocusedWindow;
        if (focused == null) return -1;

        var all = workspace.ManagedWindows.ToList();
        int pos = all.IndexOf(focused);
        if (pos <= 0) return -1;

        return pos - 1;
    }

    public ILayoutEngine GetLayoutEngine() => this;

    public void ShrinkPrimaryArea() {}
    public void ExpandPrimaryArea() {}
    public void ResetPrimaryArea() {}
    public void IncrementNumInPrimary() {}
    public void DecrementNumInPrimary() {}
}
