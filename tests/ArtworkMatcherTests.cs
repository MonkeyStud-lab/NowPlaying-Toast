using System.Text.Json;
using Xunit;

namespace NowPlayingToast;

public sealed class ArtworkMatcherTests
{
    [Fact]
    public void MatchesAppleMusicCombinedArtistAndAlbumMetadata()
    {
        using var document = JsonDocument.Parse("""
            [{"trackName":"Real Friends","artistName":"Kanye West","collectionName":"The Life of Pablo","artworkUrl100":"https://is1-ssl.mzstatic.com/pablo.jpg"}]
            """);
        Assert.Equal("https://is1-ssl.mzstatic.com/pablo.jpg",
            ArtworkMatcher.FindArtworkUrl(document.RootElement, "Real Friends", "Kanye West — The Life of Pablo", ""));
    }

    [Fact]
    public void DoesNotReplaceAlbumCoverWithAnotherAlbumsCopyOfTheSong()
    {
        using var document = JsonDocument.Parse("""
            [{"trackName":"Home","artistName":"Artist","collectionName":"Greatest Hits","artworkUrl100":"https://is1-ssl.mzstatic.com/wrong-album.jpg"}]
            """);
        Assert.Null(ArtworkMatcher.FindArtworkUrl(document.RootElement, "Home", "Artist", "Original Album"));
    }

    [Fact]
    public void RejectsPreviousSongsArtworkWhenWindowsHasAdvancedItsTitle()
    {
        using var document = JsonDocument.Parse("""
            [{"trackName":"Clique","artistName":"Kanye West, JAY-Z & Big Sean","collectionName":"Cruel Summer","artworkUrl100":"https://is1-ssl.mzstatic.com/previous.jpg"}]
            """);
        Assert.Null(ArtworkMatcher.FindArtworkUrl(document.RootElement, "RIVER", "¥$, Kanye West & Ty Dolla $ign — VULTURES 2", ""));
    }

    [Fact]
    public void RejectsWrongArtistInsteadOfTakingFirstSearchResult()
    {
        using var document = JsonDocument.Parse("""
            [
              {"trackName":"Home","artistName":"Another Artist","artworkUrl100":"https://is1-ssl.mzstatic.com/wrong.jpg"},
              {"trackName":"Home","artistName":"Expected Artist","artworkUrl100":"https://is1-ssl.mzstatic.com/right.jpg"}
            ]
            """);
        Assert.Equal("https://is1-ssl.mzstatic.com/right.jpg",
            ArtworkMatcher.FindArtworkUrl(document.RootElement, "Home", "Expected Artist", ""));
    }

    [Theory]
    [InlineData("Home (Live)")]
    [InlineData("Home - Remix")]
    [InlineData("Home (Acoustic)")]
    public void DoesNotSubstituteAnotherRecording(string otherTitle)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new[]
        {
            new { trackName = otherTitle, artistName = "Artist", artworkUrl100 = "https://is1-ssl.mzstatic.com/wrong.jpg" }
        }));
        Assert.Null(ArtworkMatcher.FindArtworkUrl(document.RootElement, "Home", "Artist", ""));
    }

    [Fact]
    public void PrefersExactAlbumAfterMatchingTitleAndArtist()
    {
        using var document = JsonDocument.Parse("""
            [
              {"trackName":"Home","artistName":"Artist","collectionName":"Greatest Hits","artworkUrl100":"https://is1-ssl.mzstatic.com/compilation.jpg"},
              {"trackName":"Home","artistName":"Artist","collectionName":"Original Album","artworkUrl100":"https://is1-ssl.mzstatic.com/album.jpg"}
            ]
            """);
        Assert.Equal("https://is1-ssl.mzstatic.com/album.jpg",
            ArtworkMatcher.FindArtworkUrl(document.RootElement, "Home", "Artist", "Original Album"));
    }

    [Fact]
    public void MatchesCaseWhitespacePunctuationAndAccents()
    {
        using var document = JsonDocument.Parse("""
            [{"trackName":"  Déjà  Vu ","artistName":"BEYONCE","artworkUrl100":"https://is1-ssl.mzstatic.com/cover.jpg"}]
            """);
        Assert.Equal("https://is1-ssl.mzstatic.com/cover.jpg",
            ArtworkMatcher.FindArtworkUrl(document.RootElement, "Déjà-Vu", "Beyoncé", ""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null,12,{}, {\"trackName\":42,\"artistName\":\"Artist\"}]")]
    [InlineData("[{\"trackName\":\"Home\",\"artistName\":\"Artist\",\"artworkUrl100\":42}]")]
    public void IgnoresMissingAndMalformedFields(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Null(ArtworkMatcher.FindArtworkUrl(document.RootElement, "Home", "Artist", ""));
    }

    [Theory]
    [InlineData("", "Artist")]
    [InlineData("Home", "")]
    [InlineData("---", "!!!")]
    public void RequiresBothTitleAndArtist(string title, string artist)
    {
        using var document = JsonDocument.Parse("""
            [{"trackName":"","artistName":"","artworkUrl100":"https://is1-ssl.mzstatic.com/cover.jpg"}]
            """);
        Assert.Null(ArtworkMatcher.FindArtworkUrl(document.RootElement, title, artist, ""));
    }

    [Theory]
    [InlineData("https://is1-ssl.mzstatic.com/image.jpg", true)]
    [InlineData("https://itunes.apple.com/image.jpg", true)]
    [InlineData("http://is1-ssl.mzstatic.com/image.jpg", false)]
    [InlineData("https://mzstatic.com.example.com/image.jpg", false)]
    [InlineData("https://evilmzstatic.com/image.jpg", false)]
    [InlineData("https://example.com/image.jpg", false)]
    [InlineData("https://user@is1-ssl.mzstatic.com/image.jpg", false)]
    [InlineData("https://is1-ssl.mzstatic.com:8443/image.jpg", false)]
    [InlineData("file:///cover.jpg", false)]
    [InlineData(null, false)]
    public void AllowsOnlyHttpsAppleArtworkHosts(string? url, bool expected)
    {
        Assert.Equal(expected, ArtworkMatcher.IsAllowedArtworkUrl(url));
    }
}
