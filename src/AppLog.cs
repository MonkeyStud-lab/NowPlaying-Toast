namespace NowPlayingToast;

internal static class AppLog
{
    private static readonly object Gate = new();

    public static void Write(string operation, Exception exception)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NowPlayingToast");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, path + ".old", overwrite: true);
                File.AppendAllText(path,
                    $"{DateTimeOffset.Now:O} {operation}: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}");
            }
        }
        catch { } // Diagnostic failures must never stop the tray app.
    }
}
