using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.Entities;

public sealed class UserTests
{
    [Fact]
    public void Rename_Should_UpdateNameAndTrimWhitespace()
    {
        // Arrange
        User sut = new() { Name = "Old Name" };

        // Act
        sut.Rename("  New Name  ");

        // Assert
        Assert.Equal("New Name", sut.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_Should_ThrowArgumentException_WhenNameIsNullOrWhitespace(string? invalidName)
    {
        // Arrange
        User sut = new() { Name = "Valid Name" };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => sut.Rename(invalidName!));
        Assert.Contains("Name is required", ex.Message);
    }

    [Fact]
    public void ChangePrimaryEmail_Should_UpdateEmailAndResetVerification_WhenEmailIsDifferent()
    {
        // Arrange
        User sut = new() { Email = Email.Parse("old@example.com"), IsEmailVerified = true };
        Email newEmail = Email.Parse("new@example.com");

        // Act
        sut.ChangePrimaryEmail(newEmail);

        // Assert
        Assert.Equal("new@example.com", sut.Email);
        Assert.False(sut.IsEmailVerified);
    }

    [Fact]
    public void ChangePrimaryEmail_Should_NotResetVerification_WhenEmailIsSame()
    {
        // Arrange
        User sut = new() { Email = Email.Parse("same@example.com"), IsEmailVerified = true };
        Email newEmail = Email.Parse("same@example.com");

        // Act
        sut.ChangePrimaryEmail(newEmail);

        // Assert
        Assert.Equal("same@example.com", sut.Email);
        Assert.True(sut.IsEmailVerified);
    }

    [Fact]
    public void ReplacePasswordHash_Should_UpdatePasswordHash()
    {
        // Arrange
        User sut = new() { PasswordHash = "old-hash" };

        // Act
        sut.ReplacePasswordHash("new-hash");

        // Assert
        Assert.Equal("new-hash", sut.PasswordHash);
    }

    [Fact]
    public void ReplacePasswordHash_Should_ThrowArgumentException_WhenHashIsNull()
    {
        User sut = User.Create("N", "a@b.c", "hash");
        Assert.Throws<ArgumentException>(() => sut.ReplacePasswordHash(null!));
    }
}
