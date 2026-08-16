using Hermes.Application.Services.Security;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Contains unit tests for <see cref="RefreshTokenHashUtility"/>, validating SHA-256 uppercase hex hashing properties.
/// </summary>
public sealed class RefreshTokenHasherTests
{
    private const string SHA_256_ABC_LOWER_HEX = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string PLAIN = "refresh-material";

    /// <summary>
    /// Tests that <see cref="RefreshTokenHashUtility.Hash"/> outputs a 64-character uppercase hexadecimal SHA-256 digest.
    /// </summary>
    [Fact]
    public void Hash_Should_ReturnUppercaseHex64_ForDeterministicSha256()
    {
        // Act
        string hashedToken = RefreshTokenHashUtility.Hash("abc");

        // Assert
        Assert.Equal(64, hashedToken.Length);
        Assert.Equal(SHA_256_ABC_LOWER_HEX.ToUpperInvariant(), hashedToken);
        Assert.Matches("^[0-9A-F]{64}$", hashedToken);
    }

    /// <summary>
    /// Tests that repeated hashing of the same plaintext string produces identical hashes.
    /// </summary>
    [Fact]
    public void Hash_Should_BeDeterministic_ForSamePlaintext()
    {
        // Act & Assert
        Assert.Equal(RefreshTokenHashUtility.Hash(PLAIN), RefreshTokenHashUtility.Hash(PLAIN));
    }

    /// <summary>
    /// Tests that distinct plaintexts yield distinct hash digests.
    /// </summary>
    [Fact]
    public void Hash_Should_Differ_ForDifferentPlaintext()
    {
        // Act & Assert
        Assert.NotEqual(RefreshTokenHashUtility.Hash("a"), RefreshTokenHashUtility.Hash("b"));
    }

    /// <summary>
    /// Tests that UTF-8 multi-byte characters are hashed accurately and not replaced with ASCII substitutions.
    /// </summary>
    [Fact]
    public void Hash_Should_UseUtf8Bytes_NotAsciiSubstitution()
    {
        // Arrange
        string umlaut = "straße";

        // Act & Assert
        Assert.NotEqual(RefreshTokenHashUtility.Hash("strasse"), RefreshTokenHashUtility.Hash(umlaut));
    }

    /// <summary>
    /// Tests that passing a null argument throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Hash_Should_ThrowArgumentNullException_WhenPlainTokenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => RefreshTokenHashUtility.Hash(null!));
    }
}
