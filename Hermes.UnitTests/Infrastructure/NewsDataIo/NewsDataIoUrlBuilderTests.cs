using Hermes.Infrastructure.NewsDataIo;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.NewsDataIo;

/// <summary>
/// Specifications for building NewsData.io request URLs: required api key, RFC 3986 query escaping, optional segments omitted when absent.
/// </summary>
public sealed class NewsDataIoUrlBuilderTests
{
    [Fact]
    public void Build_ThrowsArgumentNull_WhenPartsNull()
    {
        Assert.Throws<ArgumentNullException>(() => NewsDataIoUrlBuilder.Build(null!));
    }

    /// <summary>
    /// ApiKey must be non-whitespace — remote API always requires authentication query parameter.
    /// </summary>
    [Fact]
    public void Build_Throws_WhenApiKeyMissing()
    {
        Assert.Throws<ArgumentException>(() =>
            NewsDataIoUrlBuilder.Build(new ApiUrlParts { ApiKey = "" }));

        Assert.Throws<ArgumentException>(() =>
            NewsDataIoUrlBuilder.Build(new ApiUrlParts { ApiKey = "   " }));
    }

    /// <summary>
    /// Base path includes latest endpoint; api key value is percent-encoded (+ and & → safe query literals).
    /// </summary>
    [Fact]
    public void Build_StartsWithBaseAndEscapedApiKey()
    {
        string url = NewsDataIoUrlBuilder.Build(new ApiUrlParts { ApiKey = "key+with&ampersand" });

        Assert.StartsWith("https://newsdata.io/api/1/latest?", url, StringComparison.Ordinal);
        Assert.Contains("apikey=key%2Bwith%26ampersand", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// List parameters join with commas (comma encoded as %2C); optional scalar parameters append when provided.
    /// </summary>
    [Fact]
    public void Build_AppendsCommaSeparatedLists_AndOptionalParameters()
    {
        string url = NewsDataIoUrlBuilder.Build(new ApiUrlParts
        {
            ApiKey = "k",
            Countries = ["de", " at "],
            Languages = ["en"],
            Categories = ["technology"],
            Timezone = "europe/berlin",
            Image = 1,
            RemoveDuplicate = 0,
            Sort = "pubdateasc",
            ExcludeField = "a,b",
            Q = "climate OR energy",
        });

        Assert.Contains("country=de%2Cat", url, StringComparison.Ordinal);
        Assert.Contains("language=en", url, StringComparison.Ordinal);
        Assert.Contains("category=technology", url, StringComparison.Ordinal);
        Assert.Contains("timezone=europe%2Fberlin", url, StringComparison.Ordinal);
        Assert.Contains("image=1", url, StringComparison.Ordinal);
        Assert.Contains("removeduplicate=0", url, StringComparison.Ordinal);
        Assert.Contains("sort=pubdateasc", url, StringComparison.Ordinal);
        Assert.Contains("excludefield=a%2Cb", url, StringComparison.Ordinal);
        Assert.Contains("q=climate%20OR%20energy", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Empty segments in comma-separated lists must not emit stray commas-only groups.
    /// </summary>
    [Fact]
    public void Build_SkipsNullOrEmptyCommaSeparatedSegments()
    {
        string url = NewsDataIoUrlBuilder.Build(new ApiUrlParts
        {
            ApiKey = "k",
            Countries = ["", "  ", "fr"],
        });

        Assert.Contains("country=fr", url, StringComparison.Ordinal);
        Assert.DoesNotContain("country=%2C", url);
    }

    /// <summary>
    /// Nullable optional integers omitted from URL when null (smaller query string when unused).
    /// </summary>
    [Fact]
    public void Build_OmitsOptionalInts_WhenNull()
    {
        string url = NewsDataIoUrlBuilder.Build(new ApiUrlParts { ApiKey = "k", Image = null, RemoveDuplicate = null });

        Assert.DoesNotContain("image=", url);
        Assert.DoesNotContain("removeduplicate=", url);
    }
}
