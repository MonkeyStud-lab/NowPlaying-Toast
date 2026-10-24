using NowPlayingToast;
using Xunit;

namespace NowPlayingToast.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NowPlayingToast.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppSettingsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void MissingSettingsUseDefaultsWithoutCreatingFiles()
    {
        var missingPath = Path.Combine(_directory, "not-created", "settings.json");

        var settings = AppSettings.LoadFrom(missingPath);

        Assert.Equal(4.2, settings.HoldDurationSeconds);
        Assert.Equal(ToastCorner.BottomRight, settings.Corner);
        Assert.True(settings.ShowOnAppleMusicActivate);
        Assert.False(Directory.Exists(Path.GetDirectoryName(missingPath)));
    }

    [Theory]
    [InlineData("{ not valid json")]
    [InlineData("null")]
    [InlineData("{\"HoldDurationSeconds\":\"NaN\"}")]
    [InlineData("{\"Corner\":\"UnknownCorner\"}")]
    public void InvalidSettingsUseDefaultsAndPreserveOriginalFile(string original)
    {
        File.WriteAllText(SettingsPath, original);

        var settings = AppSettings.LoadFrom(SettingsPath);

        Assert.Equal(4.2, settings.HoldDurationSeconds);
        Assert.Equal(original, File.ReadAllText(SettingsPath));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData(double.NaN, 4.2)]
    [InlineData(double.PositiveInfinity, 4.2)]
    [InlineData(double.NegativeInfinity, 4.2)]
    [InlineData(-5.0, 1.0)]
    [InlineData(60.0, 30.0)]
    [InlineData(6.7, 6.7)]
    public void HoldDurationRemainsFiniteAndWithinSupportedRange(double value, double expected)
    {
        var settings = new AppSettings { HoldDurationSeconds = value };

        settings.Clamp();

        Assert.Equal(expected, settings.HoldDurationSeconds);
    }

    [Fact]
    public void LoadingOutOfRangeSettingsNormalizesValuesWithoutRewritingFile()
    {
        const string original = "{\"HoldDurationSeconds\":300,\"Corner\":99,\"Size\":99,\"Theme\":99,\"MonitorIndex\":-5}";
        File.WriteAllText(SettingsPath, original);

        var settings = AppSettings.LoadFrom(SettingsPath);

        Assert.Equal(30, settings.HoldDurationSeconds);
        Assert.Equal(ToastCorner.BottomRight, settings.Corner);
        Assert.Equal(ToastSize.Normal, settings.Size);
        Assert.Equal(ToastTheme.Dark, settings.Theme);
        Assert.Equal(-1, settings.MonitorIndex);
        Assert.Equal(original, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SaveReplacesExistingSettingsAndRoundTripsEveryPreference()
    {
        File.WriteAllText(SettingsPath, "old invalid settings");
        var expected = new AppSettings
        {
            HoldDurationSeconds = 8.5,
            Corner = ToastCorner.TopLeft,
            Size = ToastSize.Large,
            Theme = ToastTheme.Light,
            MonitorIndex = 2,
            ShowOnAppleMusicActivate = false,
            PlaySoundOnToast = true,
            CheckForUpdatesOnStartup = false
        };

        expected.SaveTo(SettingsPath);
        var actual = AppSettings.LoadFrom(SettingsPath);

        Assert.Equal(expected.HoldDurationSeconds, actual.HoldDurationSeconds);
        Assert.Equal(expected.Corner, actual.Corner);
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.MonitorIndex, actual.MonitorIndex);
        Assert.Equal(expected.ShowOnAppleMusicActivate, actual.ShowOnAppleMusicActivate);
        Assert.Equal(expected.PlaySoundOnToast, actual.PlaySoundOnToast);
        Assert.Equal(expected.CheckForUpdatesOnStartup, actual.CheckForUpdatesOnStartup);
        Assert.Single(Directory.GetFiles(_directory));
        Assert.DoesNotContain("SizeScale", File.ReadAllText(SettingsPath));
        Assert.Contains("\"TopLeft\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void FailedReplacementPreservesDestinationAndCleansTemporaryFile()
    {
        // A destination directory cannot be replaced by a file on Windows or Unix.
        // This forces failure after the temporary settings file has been written.
        Directory.CreateDirectory(SettingsPath);
        var marker = Path.Combine(SettingsPath, "previous-settings");
        File.WriteAllText(marker, "preserve me");

        Assert.ThrowsAny<IOException>(() => new AppSettings().SaveTo(SettingsPath));

        Assert.Equal("preserve me", File.ReadAllText(marker));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void LockedSettingsFileOnWindowsIsPreservedWhenReplacementFails()
    {
        // Unix permits replacement of open files. Windows applies the sharing rules
        // used by this test; the Windows build workflow exercises this path.
        if (!OperatingSystem.IsWindows()) return;
        const string original = "{\"HoldDurationSeconds\":6.5}";
        File.WriteAllText(SettingsPath, original);

        using (var lockedFile = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => new AppSettings().SaveTo(SettingsPath));

        Assert.Equal(original, File.ReadAllText(SettingsPath));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void EditingACloneDoesNotChangeLiveSettings()
    {
        var original = new AppSettings();
        var edited = original.Clone();

        edited.HoldDurationSeconds = 15;
        edited.Corner = ToastCorner.TopLeft;
        edited.PlaySoundOnToast = true;

        Assert.Equal(4.2, original.HoldDurationSeconds);
        Assert.Equal(ToastCorner.BottomRight, original.Corner);
        Assert.False(original.PlaySoundOnToast);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
