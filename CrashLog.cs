using System.Runtime.InteropServices;

namespace TXT2IMG;

// WinUI3 fails fast (silent process termination, no dialog) when an exception
// crosses back into the native message loop instead of showing the usual .NET
// "Unhandled exception" behavior. MessageBoxW is used here specifically because
// it doesn't depend on XAML/WinRT being in a working state, unlike a XAML dialog.
internal static class CrashLog
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    public static void ReportFatal(Exception? ex)
    {
        var message = ex?.ToString() ?? "Unknown fatal error (no exception object).";

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), $"{DateTime.Now:O}\n{message}\n");
        }
        catch
        {
            // Best-effort logging only - must not mask the original crash.
        }

        MessageBoxW(0, message, "TXT2IMG - Fatal Error", 0x10 /* MB_ICONERROR */);
    }
}
