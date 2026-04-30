using Hermes.Application.Security;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Specifications for refresh-token hashing: deterministic SHA-256 hex output, UTF-8 semantics, and clear failures on bad input.
/// </summary>
public sealed class RefreshTokenHasherTests
{
    private const string Sha256AbcLowerHex = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    /// <summary>
    /// Hash output must be 64 uppercase hex chars matching SHA-256 of the plaintext (deterministic reference vector for <c>abc</c>).
    /// </summary>
    [Fact]
    public void Hash_Should_ReturnUppercaseHex64_ForDeterministicSha256()
    {
        // Act
        string h = RefreshTokenHasher.Hash("abc");

        // Assert
        Assert.Equal(64, h.Length);
        Assert.Equal(Sha256AbcLowerHex.ToUpperInvariant(), h);
        Assert.Matches("^[0-9A-F]{64}$", h);
    }

    /// <summary>
    /// Same plaintext must always yield the same stored hash (idempotent hashing for lookups).
    /// </summary>
    [Fact]
    public void Hash_Should_BeDeterministic_ForSamePlaintext()
    {
        // Arrange
        const string plain = "refresh-material";

        // Act / Assert
        Assert.Equal(RefreshTokenHasher.Hash(plain), RefreshTokenHasher.Hash(plain));
    }

    /// <summary>
    /// Different plaintexts must produce different hashes (collision resistance at application level).
    /// </summary>
    [Fact]
    public void Hash_Should_Differ_ForDifferentPlaintext()
    {
        Assert.NotEqual(RefreshTokenHasher.Hash("a"), RefreshTokenHasher.Hash("b"));
    }

    /// <summary>
    /// Hashing uses UTF-8 bytes, so visually similar ASCII vs Unicode strings must not collide.
    /// </summary>
    [Fact]
    public void Hash_Should_UseUtf8Bytes_NotAsciiSubstitution()
    {
        // Arrange
        string umlaut = "straße";

        // Act / Assert
        Assert.NotEqual(RefreshTokenHasher.Hash("strasse"), RefreshTokenHasher.Hash(umlaut));
    }

    /// <summary>
    /// Null plaintext is rejected explicitly (<see cref="ArgumentNullException"/>).
    /// </summary>
    [Fact]
    public void Hash_Should_ThrowArgumentNull_WhenPlainTokenNull()
    {
        Assert.Throws<ArgumentNullException>(() => RefreshTokenHasher.Hash(null!));
    }
}
