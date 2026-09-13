using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NowPlayingToast;

internal static class NativeMethods
{
    public const int SW_RESTORE = 9;
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    public static bool FocusAppleMusic()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("AppleMusic"))
            {
                var hwnd = proc.MainWindowHandle;
                if (hwnd == IntPtr.Zero) continue;

                if (IsIconic(hwnd))
                    ShowWindow(hwnd, SW_RESTORE);

                var foreground = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                uint appThread = GetCurrentThreadId();
                bool attached = false;
                if (fgThread != 0 && fgThread != appThread)
                    attached = AttachThreadInput(appThread, fgThread, true);

                try
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
                finally
                {
                    if (attached)
                        AttachThreadInput(appThread, fgThread, false);
                }

                return true;
            }
        }
        catch { }

        return false;
    }
}
