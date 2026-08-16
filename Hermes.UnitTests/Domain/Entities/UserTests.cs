using Hermes.Domain.Entities;
using Hermes.Domain.Events;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.Entities;

/// <summary>
/// Contains comprehensive unit tests for the <see cref="User"/> aggregate root,
/// verifying state transitions, domain invariants, and domain event dispatching.
/// </summary>
public sealed class UserTests
{
    /// <summary>
    /// Tests that the factory method <see cref="User.Create"/> successfully instantiates a new user,
    /// sets initial state (unverified email), and registers a <see cref="UserRegisteredEvent"/>.
    /// </summary>
    [Fact]
    public void Create_Should_InitializePropertiesAndAddDomainEvent_WhenParametersAreValid()
    {
        // Arrange
        string name = "  Max Mustermann  ";
        Email email = Email.Parse("max@hermes.de");
        string passwordHash = "$2a$11$samplebcryptpasswordhash";

        // Act
        User user = User.Create(name, email, passwordHash);

        // Assert
        Assert.Equal("Max Mustermann", user.Name);
        Assert.Equal(email, user.Email);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.TwoFactorCode);
        Assert.Null(user.TwoFactorExpiry);
        Assert.Empty(user.NewsletterSubscriptions);
        Assert.Empty(user.NotificationLogs);
        Assert.Empty(user.RefreshTokens);

        var registeredEvent = Assert.Single(user.DomainEvents);
        var typedEvent = Assert.IsType<UserRegisteredEvent>(registeredEvent);
        Assert.Equal(email.Value, typedEvent.Email);
    }

    /// <summary>
    /// Tests that <see cref="User.Create"/> throws a <see cref="DomainValidationException"/>
    /// when the user name is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ThrowDomainValidationException_WhenNameIsNullOrWhitespace(string? invalidName)
    {
        // Arrange
        Email email = Email.Parse("user@hermes.de");
        string passwordHash = "hash123";

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() => User.Create(invalidName!, email, passwordHash));
        Assert.Contains("Name is required", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="User.Create"/> throws a <see cref="DomainValidationException"/>
    /// when the password hash is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_ThrowDomainValidationException_WhenPasswordHashIsNullOrWhitespace(string? invalidHash)
    {
        // Arrange
        Email email = Email.Parse("user@hermes.de");

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() => User.Create("Valid Name", email, invalidHash!));
        Assert.Contains("PasswordHash is required", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="User.Rename"/> updates the name and trims extraneous whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="User.Rename"/> throws a <see cref="DomainValidationException"/>
    /// when the new name is null or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_Should_ThrowArgumentException_WhenNameIsNullOrWhitespace(string? invalidName)
    {
        // Arrange
        User sut = new() { Name = "Valid Name" };

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => sut.Rename(invalidName!));
        Assert.Contains("Name is required", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="User.ChangePrimaryEmail"/> resets email verification to false
    /// and dispatches a <see cref="UserEmailChangedEvent"/> when the new email differs from the previous one.
    /// </summary>
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
        var emailChangedEvent = Assert.Single(sut.DomainEvents);
        var typedEvent = Assert.IsType<UserEmailChangedEvent>(emailChangedEvent);
        Assert.Equal("old@example.com", typedEvent.OldEmail);
        Assert.Equal("new@example.com", typedEvent.NewEmail);
    }

    /// <summary>
    /// Tests that <see cref="User.ChangePrimaryEmail"/> does not reset verification status
    /// and does not dispatch a domain event when the new email equals the current email (case-insensitive).
    /// </summary>
    [Fact]
    public void ChangePrimaryEmail_Should_NotResetVerificationOrAddEvent_WhenEmailIsSame()
    {
        // Arrange
        User sut = new() { Email = Email.Parse("same@example.com"), IsEmailVerified = true };
        Email newEmail = Email.Parse("SAME@example.com");

        // Act
        sut.ChangePrimaryEmail(newEmail);

        // Assert
        Assert.Equal("same@example.com", sut.Email);
        Assert.True(sut.IsEmailVerified);
        Assert.Empty(sut.DomainEvents);
    }

    /// <summary>
    /// Tests that <see cref="User.VerifyEmail"/> updates <see cref="User.IsEmailVerified"/> to true.
    /// </summary>
    [Fact]
    public void VerifyEmail_Should_SetIsEmailVerifiedToTrue()
    {
        // Arrange
        User sut = new() { IsEmailVerified = false };

        // Act
        sut.VerifyEmail();

        // Assert
        Assert.True(sut.IsEmailVerified);
    }

    /// <summary>
    /// Tests that <see cref="User.ReplacePasswordHash"/> replaces the stored hash with a new valid hash value.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="User.ReplacePasswordHash"/> throws a <see cref="DomainValidationException"/>
    /// when the new hash is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReplacePasswordHash_Should_ThrowDomainValidationException_WhenHashIsNullOrWhitespace(string? invalidHash)
    {
        // Arrange
        User sut = User.Create("N", Email.Parse("a@b.c"), "hash");

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() => sut.ReplacePasswordHash(invalidHash!));
        Assert.Contains("Password hash cannot be empty", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="User.EnableTwoFactor"/> sets the two-factor authentication code and expiration timestamp.
    /// </summary>
    [Fact]
    public void EnableTwoFactor_Should_SetCodeAndExpiry_WhenCodeIsValid()
    {
        // Arrange
        User sut = new();
        DateTime expiry = DateTime.UtcNow.AddMinutes(15);

        // Act
        sut.EnableTwoFactor("123456", expiry);

        // Assert
        Assert.Equal("123456", sut.TwoFactorCode);
        Assert.Equal(expiry, sut.TwoFactorExpiry);
    }

    /// <summary>
    /// Tests that <see cref="User.EnableTwoFactor"/> throws a <see cref="DomainValidationException"/>
    /// when the code is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnableTwoFactor_Should_ThrowDomainValidationException_WhenCodeIsNullOrWhitespace(string? invalidCode)
    {
        // Arrange
        User sut = new();

        // Act & Assert
        var ex = Assert.Throws<DomainValidationException>(() => sut.EnableTwoFactor(invalidCode!, DateTime.UtcNow.AddMinutes(10)));
        Assert.Contains("Code cannot be empty", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="User.DisableTwoFactor"/> resets both the code and expiry properties to null.
    /// </summary>
    [Fact]
    public void DisableTwoFactor_Should_ClearCodeAndExpiry()
    {
        // Arrange
        User sut = new()
        {
            TwoFactorCode = "654321",
            TwoFactorExpiry = DateTime.UtcNow.AddMinutes(10)
        };

        // Act
        sut.DisableTwoFactor();

        // Assert
        Assert.Null(sut.TwoFactorCode);
        Assert.Null(sut.TwoFactorExpiry);
    }
}
