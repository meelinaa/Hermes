using Hermes.Application.Security;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class RefreshTokenHasherTests
{
    private const string SHA_256_ABC_LOWER_HEX = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string PLAIN = "refresh-material";

    [Fact]
    public void Hash_Should_ReturnUppercaseHex64_ForDeterministicSha256()
    {
        string hashedToken = RefreshTokenHashService.Hash("abc");
        Assert.Equal(64, hashedToken.Length);
        Assert.Equal(SHA_256_ABC_LOWER_HEX.ToUpperInvariant(), hashedToken);
        Assert.Matches("^[0-9A-F]{64}$", hashedToken);
    }

    [Fact]
    public void Hash_Should_BeDeterministic_ForSamePlaintext() => Assert.Equal(RefreshTokenHashService.Hash(PLAIN), RefreshTokenHashService.Hash(PLAIN));

    [Fact]
    public void Hash_Should_Differ_ForDifferentPlaintext() => Assert.NotEqual(RefreshTokenHashService.Hash("a"), RefreshTokenHashService.Hash("b"));

    [Fact]
    public void Hash_Should_UseUtf8Bytes_NotAsciiSubstitution()
    {
        string umlaut = "straße";
        Assert.NotEqual(RefreshTokenHashService.Hash("strasse"), RefreshTokenHashService.Hash(umlaut));
    }

    [Fact]
    public void Hash_Should_ThrowArgumentNull_WhenPlainTokenNull() => Assert.Throws<ArgumentNullException>(() => RefreshTokenHashService.Hash(null!));
}
