using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace workspacer
{
    public sealed class AltDrag : IDisposable
    {
        private static Logger _logger = Logger.Create();
        private const int MinWindowWidth  = 120;
        private const int MinWindowHeight = 60;

        private const byte VK_LWIN = 0x5B;
        private const byte VK_RWIN = 0x5C;

        private const uint WM_KEYDOWN    = 0x0100;
        private const uint WM_KEYUP      = 0x0101;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP   = 0x0105;

        private enum DragMode { None, Move, Resize }

        private DragMode   _mode = DragMode.None;
        private IntPtr     _hwnd = IntPtr.Zero;
        private IWindow    _window;
        private Point      _startMouse;
        private Win32.Rect _startRect;

        private ConfigContext _context;
        private WindowsManager _windows;

        // Resize: which quadrant was clicked.
        // -1 = left/top edge moves, +1 = right/bottom edge moves.
        private int _edgeX;
        private int _edgeY;

        // Win-key suppression state
        private bool _winKeyClean; // true while Win held and no drag has fired yet

        private readonly KeyModifiers   _modifiers;
        private readonly Win32.HookProc _mouseHookProc;
        private readonly IntPtr         _mouseHook;
        private readonly Win32.HookProc _kbdHookProc;
        private readonly IntPtr         _kbdHook;

        public AltDrag(ConfigContext context, KeyModifiers modifiers = KeyModifiers.Alt)
        {
            _context = context;
            _windows = _context.Windows;
            _modifiers     = modifiers;
            _mouseHookProc = MouseHookCallback;
            _mouseHook     = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseHookProc, IntPtr.Zero, 0);

            if (_mouseHook == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to install mouse hook. Error: {Marshal.GetLastWin32Error()}");

            // Install keyboard hook only when Win is part of the modifier set
            if (_modifiers.HasFlag(KeyModifiers.Win))
            {
                _kbdHookProc = KeyboardHookCallback;
                _kbdHook     = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _kbdHookProc, IntPtr.Zero, 0);

                if (_kbdHook == IntPtr.Zero)
                    throw new InvalidOperationException(
                        $"Failed to install keyboard hook. Error: {Marshal.GetLastWin32Error()}");
            }
        }

        public void Dispose()
        {
            if (_mouseHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mouseHook);
            if (_kbdHook   != IntPtr.Zero) Win32.UnhookWindowsHookEx(_kbdHook);
        }

        private IntPtr KeyboardHookCallback(int code, UIntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                var msg = (uint)wParam;
                // First field of KBDLLHOOKSTRUCT is vkCode (DWORD)
                var vk  = (byte)Marshal.ReadInt32(lParam);

                bool isWinKey = vk == VK_LWIN || vk == VK_RWIN;

                if (isWinKey)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        // If a drag is already active, suppress Win key immediately
                        if (_mode != DragMode.None)
                        {
                            return new IntPtr(1);
                        }

                        _winKeyClean = true;
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        bool wasClean = _winKeyClean;
                        _winKeyClean = false;
                        EndDrag();

                        if (!wasClean)
                        {
                            return new IntPtr(1);
                        }
                    }
                }
            }

            return Win32.CallNextHookEx(_kbdHook, code, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int code, UIntPtr wParam, IntPtr lParam)
        {
            if (code < 0)
                return Win32.CallNextHookEx(_mouseHook, code, wParam, lParam);

            var  msg      = (uint)wParam;
            var  hookData = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            var  cursor   = new Point(hookData.pt.X, hookData.pt.Y);
            bool suppress = false;

            switch (msg)
            {
                case var m when m == Win32.WM_LBUTTONDOWN && ModifiersActive():
                    suppress = TryBeginDrag(cursor, DragMode.Move);
                    break;

                case var m when m == Win32.WM_RBUTTONDOWN && ModifiersActive():
                    suppress = TryBeginDrag(cursor, DragMode.Resize);
                    break;

                case var m when m == Win32.WM_LBUTTONUP:
                    if (_mode == DragMode.Move)
                    {
                        EndDrag(); 
                        suppress = true;
                    }
                    break;

                case var m when m == Win32.WM_RBUTTONUP:
                    if (_mode == DragMode.Resize)
                    {
                        EndDrag(); 
                        suppress = true;
                    }
                    break;

                case var m when m == Win32.WM_MOUSEMOVE:
                    if (_mode != DragMode.None) UpdateDrag(cursor);
                    break;
            }

            return suppress
                ? new IntPtr(1)
                : Win32.CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        private bool TryBeginDrag(Point cursor, DragMode mode)
        {
            var hwnd = Win32.RootWindowFromPoint(new Win32.POINT { X = cursor.X, Y = cursor.Y });
            if (hwnd == IntPtr.Zero) return false;

            if (!Win32Helper.IsAppWindow(hwnd) || Win32Helper.IsCloaked(hwnd)) return false;

            if (Win32.IsZoomed(hwnd))
                Win32.ShowWindow(hwnd, Win32.SW.SW_RESTORE);

            var rect = new Win32.Rect();
            if (!Win32.GetWindowRect(hwnd, ref rect)) return false;

            _hwnd       = hwnd;
            _window = GetWindow();
            _mode       = mode;
            _startMouse = cursor;
            _startRect  = rect;

            if (mode == DragMode.Resize)
                CalculateResizeEdges(cursor, rect);

            // Mark Win key as consumed so its keyup will be suppressed
            _winKeyClean = false;

            Win32Helper.ForceForegroundWindow(hwnd);
            _windows.StartWindowMove(_hwnd);
            return true;
        }

        private void CalculateResizeEdges(Point cursor, Win32.Rect rect)
        {
            int midX = (rect.Left + rect.Right)  / 2;
            int midY = (rect.Top  + rect.Bottom) / 2;

            _edgeX = cursor.X < midX ? -1 : 1;
            _edgeY = cursor.Y < midY ? -1 : 1;
        }

        private void UpdateDrag(Point cursor)
        {
            if (_hwnd == IntPtr.Zero) return;

            int dx = cursor.X - _startMouse.X;
            int dy = cursor.Y - _startMouse.Y;

            if (_mode == DragMode.Move) ApplyMove(dx, dy);
            else                        ApplyResize(dx, dy);
        }

        private void ApplyMove(int dx, int dy)
        {
            Win32.SetWindowPos(
                _hwnd, IntPtr.Zero,
                _startRect.Left + dx,
                _startRect.Top  + dy,
                0, 0,
                Win32.SetWindowPosFlags.IgnoreResize          |
                Win32.SetWindowPosFlags.IgnoreZOrder          |
                Win32.SetWindowPosFlags.DoNotActivate         |
                Win32.SetWindowPosFlags.DoNotSendChangingEvent);
            _windows.WindowMove(_hwnd);
        }

        private void ApplyResize(int dx, int dy)
        {
            int left   = _startRect.Left;
            int top    = _startRect.Top;
            int right  = _startRect.Right;
            int bottom = _startRect.Bottom;

            LocationLockAxis locked = _window?.TilePosition?.LockedAxis ?? LocationLockAxis.None;

            int effectiveEdgeX = _edgeX;
            int effectiveEdgeY = _edgeY;

            bool leftLocked  = locked.HasFlag(LocationLockAxis.Left);
            bool rightLocked = locked.HasFlag(LocationLockAxis.Right);
            bool topLocked   = locked.HasFlag(LocationLockAxis.Top);
            bool botLocked   = locked.HasFlag(LocationLockAxis.Bottom);

            if (_edgeX == -1 && leftLocked  && !rightLocked) effectiveEdgeX =  1;
            if (_edgeX ==  1 && rightLocked && !leftLocked)  effectiveEdgeX = -1;
            if (_edgeX == -1 && leftLocked  && rightLocked)  effectiveEdgeX =  0; // both locked, skip
            if (_edgeX ==  1 && rightLocked && leftLocked)   effectiveEdgeX =  0;

            if (_edgeY == -1 && topLocked && !botLocked) effectiveEdgeY =  1;
            if (_edgeY ==  1 && botLocked && !topLocked) effectiveEdgeY = -1;
            if (_edgeY == -1 && topLocked && botLocked)  effectiveEdgeY =  0;
            if (_edgeY ==  1 && botLocked && topLocked)  effectiveEdgeY =  0;

            if (effectiveEdgeX == -1) left   += dx;
            else if (effectiveEdgeX ==  1) right  += dx;

            if (effectiveEdgeY == -1) top    += dy;
            else if (effectiveEdgeY ==  1) bottom += dy;

            int w = Math.Max(right  - left, MinWindowWidth);
            int h = Math.Max(bottom - top,  MinWindowHeight);

            int finalLeft = (_edgeX == -1) ? right  - w : left;
            int finalTop  = (_edgeY == -1) ? bottom - h : top;

            Win32.SetWindowPos(
                _hwnd, IntPtr.Zero,
                finalLeft, finalTop, w, h,
                Win32.SetWindowPosFlags.IgnoreZOrder          |
                Win32.SetWindowPosFlags.DoNotActivate         |
                Win32.SetWindowPosFlags.DoNotSendChangingEvent);
            
            _windows.WindowMove(_hwnd);
        }

        private void EndDrag()
        {
            _windows.EndWindowMove(_hwnd);
            _mode = DragMode.None;
            _hwnd = IntPtr.Zero;
            _window = null;
        }

        private bool ModifiersActive()
        {
            if (_modifiers.HasFlag(KeyModifiers.Alt)     && !KeyDown(Keys.Menu))       return false;
            if (_modifiers.HasFlag(KeyModifiers.Control) && !KeyDown(Keys.ControlKey)) return false;
            if (_modifiers.HasFlag(KeyModifiers.Shift)   && !KeyDown(Keys.ShiftKey))   return false;

            // Win keydown is still passed through by our hook, so GetKeyState works normally
            if (_modifiers.HasFlag(KeyModifiers.Win) &&
                !KeyDown(Keys.LWin) && !KeyDown(Keys.RWin)) return false;

            return true;
        }

        private IWindow GetWindow()
        {
            return _context.Windows.FromHWND(_hwnd);
        }

        private static bool KeyDown(Keys key) =>
            (Win32.GetKeyState((System.Windows.Forms.Keys) key) & 0x8000) != 0;
        
    }
}
