using System;
using System.Runtime.InteropServices;

namespace workspacer
{
    public static partial class Win32
    {
        /// <summary>
        /// Low-level mouse hook data passed to WH_MOUSE_LL callbacks.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT     pt;
            public uint      mouseData;   // hi-word = wheel delta / x-button id
            public uint      flags;
            public uint      time;
            public UIntPtr   dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// Retrieves the handle of the window at the given screen point.
        /// Unlike WindowFromPoint this returns the top-level (root) window.
        /// </summary>
        public static IntPtr RootWindowFromPoint(POINT pt)
        {
            var child = WindowFromPoint(new System.Drawing.Point(pt.X, pt.Y));
            return child == IntPtr.Zero ? IntPtr.Zero : GetAncestor(child, GA.GA_ROOT);
        }
    }
}
