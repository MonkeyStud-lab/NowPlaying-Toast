using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NowPlayingToast;

internal static class ArtworkMatcher
{
    public static string? FindArtworkUrl(JsonElement results, string title, string artist, string album)
    {
        if (results.ValueKind != JsonValueKind.Array) return null;
        var expectedTitle = Normalize(title);
        var expectedArtist = Normalize(artist);
        var expectedAlbum = Normalize(album);
        if (expectedTitle.Length == 0 || expectedArtist.Length == 0) return null;

        string? bestUrl = null;
        int bestScore = -1;
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object) continue;
            if (Normalize(GetString(result, "trackName")) != expectedTitle ||
                Normalize(GetString(result, "artistName")) != expectedArtist) continue;

            var url = GetString(result, "artworkUrl100");
            if (!IsAllowedArtworkUrl(url)) continue;

            var score = expectedAlbum.Length > 0 && Normalize(GetString(result, "collectionName")) == expectedAlbum ? 1 : 0;
            if (score > bestScore)
            {
                bestScore = score;
                bestUrl = url;
            }
        }
        return bestUrl;
    }

    public static bool IsAllowedArtworkUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0) return false;

        var host = uri.IdnHost;
        return host.Equals("mzstatic.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".mzstatic.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("itunes.apple.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".itunes.apple.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string Normalize(string value)
    {
        var normalized = new StringBuilder();
        bool separator = false;
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (separator && normalized.Length > 0) normalized.Append(' ');
                normalized.Append(char.ToUpperInvariant(character));
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        // Version words such as Live, Remix, and Acoustic are deliberately retained.
        return normalized.ToString();
    }
}
