using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NowPlayingToast;

internal static class NativeMethods
{
    public const int SW_RESTORE = 9;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr handle);

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
        Process[] processes = Array.Empty<Process>();
        try
        {
            processes = Process.GetProcessesByName("AppleMusic");
            foreach (var proc in processes)
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
                    if (SetForegroundWindow(hwnd))
                        return true;
                }
                finally
                {
                    if (attached)
                        AttachThreadInput(appThread, fgThread, false);
                }
            }
        }
        catch { }
        finally
        {
            // Dispose every result, including those after an early successful return.
            foreach (var process in processes)
                process.Dispose();
        }

        return false;
    }
}
