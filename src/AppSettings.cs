using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace NowPlayingToast;

public enum ToastCorner
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft
}

public enum ToastSize
{
    Small,
    Normal,
    Large
}

public enum ToastTheme
{
    Dark,
    Light
}

public sealed partial class AppSettings
{
    public double HoldDurationSeconds { get; set; } = 4.2;
    public ToastCorner Corner { get; set; } = ToastCorner.BottomRight;
    public ToastSize Size { get; set; } = ToastSize.Normal;
    public bool ShowOnAppleMusicActivate { get; set; } = true;
    public bool PlaySoundOnToast { get; set; } = false;
    public ToastTheme Theme { get; set; } = ToastTheme.Dark;
    /// <summary>-1 = primary screen; otherwise Screen.AllScreens index.</summary>
    public int MonitorIndex { get; set; } = -1;
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NowPlayingToast");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load() => LoadFrom(SettingsPath);

    internal static AppSettings LoadFrom(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Clamp();
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        {
            // Keep unreadable or invalid files intact. Opening the app must not overwrite them.
        }

        return new AppSettings();
    }

    public void Save() => SaveTo(SettingsPath);

    internal void SaveTo(string path)
    {
        Clamp();
        var json = JsonSerializer.Serialize(this, JsonOptions);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Flush a complete file before replacing the previous settings on the same volume.
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public void Clamp()
    {
        HoldDurationSeconds = double.IsFinite(HoldDurationSeconds)
            ? Math.Clamp(HoldDurationSeconds, 1.0, 30.0)
            : 4.2;
        if (!Enum.IsDefined(Corner)) Corner = ToastCorner.BottomRight;
        if (!Enum.IsDefined(Size)) Size = ToastSize.Normal;
        if (!Enum.IsDefined(Theme)) Theme = ToastTheme.Dark;
        if (MonitorIndex < -1) MonitorIndex = -1;
    }

    [JsonIgnore]
    public float SizeScale => Size switch
    {
        ToastSize.Small => 0.85f,
        ToastSize.Large => 1.2f,
        _ => 1.0f
    };
}

