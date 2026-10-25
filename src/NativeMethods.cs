using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FrostGlass
{
    internal static class NativeMethods
    {
        public const int WCA_ACCENT_POLICY = 19;
        public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const uint LWA_ALPHA = 0x00000002;

        public const int WM_NCHITTEST = 0x0084;
        public const int HTTRANSPARENT = -1;

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const int WM_HOTKEY = 0x0312;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWCP_ROUND = 2;

        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        public const int HOTKEY_TOGGLE = 0x4D31;
        public const int HOTKEY_RESELECT = 0x4D32;

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return;
            try
            {
                IntPtr foreground = GetForegroundWindow();
                uint foregroundThread = foreground == IntPtr.Zero
                    ? 0
                    : GetWindowThreadProcessId(foreground, IntPtr.Zero);
                uint currentThread = GetCurrentThreadId();
                bool attached = false;
                if (foregroundThread != 0 && foregroundThread != currentThread)
                    attached = AttachThreadInput(currentThread, foregroundThread, true);
                try
                {
                    ShowWindow(hWnd, 5);
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    SetFocus(hWnd);
                }
                finally
                {
                    if (attached)
                        AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint colorKey, byte alpha, uint flags);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmFlush();

        public static int ApplyAcrylic(IntPtr hWnd, int alpha, Color tint)
        {
            int gradient = (alpha << 24) | (tint.B << 16) | (tint.G << 8) | tint.R;
            AccentPolicy policy = new AccentPolicy();
            policy.AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND;
            policy.AccentFlags = 2;
            policy.GradientColor = gradient;
            policy.AnimationId = 0;

            int size = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                WindowCompositionAttributeData data = new WindowCompositionAttributeData();
                data.Attribute = WCA_ACCENT_POLICY;
                data.Data = ptr;
                data.SizeOfData = size;
                return SetWindowCompositionAttribute(hWnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static void SetInputTransparent(IntPtr hWnd, bool transparent)
        {
            int style = GetWindowLong(hWnd, GWL_EXSTYLE);
            if (transparent)
                style |= WS_EX_TRANSPARENT;
            else
                style &= ~WS_EX_TRANSPARENT;
            SetWindowLong(hWnd, GWL_EXSTYLE, style);
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void TryEnableRoundedCorners(IntPtr hWnd)
        {
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
            }
        }
    }
}
