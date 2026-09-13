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

public sealed class AppSettings
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

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Clamp();
                    return loaded;
                }
            }
        }
        catch { }

        var defaults = new AppSettings();
        defaults.Save();
        return defaults;
    }

    public void Save()
    {
        Clamp();
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void Clamp()
    {
        if (HoldDurationSeconds < 1.0) HoldDurationSeconds = 1.0;
        if (HoldDurationSeconds > 30.0) HoldDurationSeconds = 30.0;
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

    public Screen ResolveScreen()
    {
        var screens = Screen.AllScreens;
        if (MonitorIndex >= 0 && MonitorIndex < screens.Length)
            return screens[MonitorIndex];
        return Screen.PrimaryScreen ?? screens[0];
    }
}

