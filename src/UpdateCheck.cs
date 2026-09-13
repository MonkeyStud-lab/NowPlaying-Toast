using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NowPlayingToast;

internal sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public string? LatestTag { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? Message { get; init; }
}

internal static class UpdateCheck
{
    public const string RepoOwner = "MonkeyStud-lab";
    public const string RepoName = "NowPlaying-Toast";
    public const string GitHubUrl = "https://github.com/MonkeyStud-lab/NowPlaying-Toast";
    public const string ReleasesApi = "https://api.github.com/repos/MonkeyStud-lab/NowPlaying-Toast/releases/latest";

    public static Version GetAssemblyVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v ?? new Version(1, 1, 0, 0);
    }

    public static string GetDisplayVersion()
    {
        var v = GetAssemblyVersion();
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NowPlayingToast", GetDisplayVersion()));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json = await http.GetStringAsync(ReleasesApi);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagEl))
                return new UpdateCheckResult { Message = "No release tag found." };

            var tag = tagEl.GetString() ?? "";
            var url = root.TryGetProperty("html_url", out var urlEl)
                ? urlEl.GetString()
                : GitHubUrl + "/releases";

            var latest = ParseVersion(tag);
            var current = GetAssemblyVersion();
            if (latest is null)
                return new UpdateCheckResult { LatestTag = tag, ReleaseUrl = url, Message = "Could not parse latest version." };

            var newer = latest.CompareTo(Normalize(current)) > 0;
            return new UpdateCheckResult
            {
                UpdateAvailable = newer,
                LatestTag = tag,
                ReleaseUrl = url,
                Message = newer
                    ? $"Update available: {tag} (you have {GetDisplayVersion()})"
                    : $"Up to date ({GetDisplayVersion()})"
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { Message = "Update check skipped: " + ex.Message };
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    private static Version? ParseVersion(string tag)
    {
        var m = Regex.Match(tag, @"(\d+)\.(\d+)(?:\.(\d+))?");
        if (!m.Success) return null;
        int major = int.Parse(m.Groups[1].Value);
        int minor = int.Parse(m.Groups[2].Value);
        int build = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        return new Version(major, minor, build);
    }
}
