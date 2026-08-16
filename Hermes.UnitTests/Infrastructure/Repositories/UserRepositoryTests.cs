using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Contains unit tests for <see cref="UserRepository"/> using an in-memory database,
/// verifying CRUD operations, unique email constraints, authentication queries, and 2FA verification workflows.
/// </summary>
public sealed class UserRepositoryTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.SetUserAsync"/> throws <see cref="ArgumentNullException"/>
    /// when the provided user entity is null.
    /// </summary>
    [Fact]
    public async Task SetUserAsync_Should_ThrowArgumentNullException_WhenUserIsNull()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetUserAsync(null!).AsTask());
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.SetUserAsync"/> throws <see cref="ArgumentException"/>
    /// when the user entity has a non-zero identifier prior to insertion.
    /// </summary>
    [Fact]
    public async Task SetUserAsync_Should_ThrowArgumentException_WhenUserIdIsNotZero()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);
        User user = new()
        {
            Id = new UserId(42),
            Name = "Existing",
            Email = Email.Parse("existing@test.dev"),
            PasswordHash = "hash"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetUserAsync(user).AsTask());
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.SetUserAsync"/> throws <see cref="EmailAlreadyExistsException"/>
    /// when another user with the same email address already exists.
    /// </summary>
    [Fact]
    public async Task SetUserAsync_Should_ThrowEmailAlreadyExistsException_WhenEmailDuplicate()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        User firstUser = new()
        {
            Id = new UserId(1),
            Name = "First",
            Email = Email.Parse("dup@test.dev"),
            PasswordHash = "hash1"
        };
        ctx.Users.Add(firstUser);
        await ctx.SaveChangesAsync();

        User newUser = new()
        {
            Id = new UserId(0),
            Name = "Second",
            Email = Email.Parse("dup@test.dev"),
            PasswordHash = "hash2"
        };

        // Act & Assert
        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => sut.SetUserAsync(newUser).AsTask());
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.SetUserAsync"/> successfully inserts a valid new user entity.
    /// </summary>
    [Fact]
    public async Task SetUserAsync_Should_InsertUser_WhenValid()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        User user = new()
        {
            Id = new UserId(0),
            Name = "ValidUser",
            Email = Email.Parse("valid@test.dev"),
            PasswordHash = "hash123"
        };

        // Act
        await sut.SetUserAsync(user);

        // Assert
        User? saved = await ctx.Users.FirstOrDefaultAsync(u => u.Name == "ValidUser");
        Assert.NotNull(saved);
        Assert.Equal("valid@test.dev", saved!.Email);
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.GetUserByNameAsync"/> returns the user scope DTO
    /// when the user exists and throws <see cref="ArgumentException"/> for invalid names.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUserByNameAsync_Should_ThrowArgumentException_WhenNameIsInvalid(string? name)
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByNameAsync(name!).AsTask());
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.GetUserByNameAsync"/> returns null when the requested username is not found.
    /// </summary>
    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnNull_WhenUserNotFound()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        // Act
        UserScopeDto? result = await sut.GetUserByNameAsync("NonExistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.GetUserByEmailAsync"/> returns the matching user scope DTO.
    /// </summary>
    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnUserScope_WhenFound()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        User user = new()
        {
            Id = new UserId(10),
            Name = "EmailUser",
            Email = Email.Parse("lookup@test.dev"),
            PasswordHash = "hash"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        UserScopeDto? result = await sut.GetUserByEmailAsync("lookup@test.dev");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result!.UserId);
        Assert.Equal("EmailUser", result.Name);
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.GetUserByIdAsync"/> throws <see cref="ArgumentOutOfRangeException"/>
    /// when the provided user identifier is zero or negative.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUserByIdAsync_Should_ThrowArgumentOutOfRangeException_WhenIdNotPositive(int invalidId)
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetUserByIdAsync(new UserId(invalidId)).AsTask());
    }

    /// <summary>
    /// Tests authentication query methods: name, email, and ID entity retrieval.
    /// </summary>
    [Fact]
    public async Task GetUserEntityForAuthentication_Should_ReturnUser_WhenCriteriaMatch()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        User user = new()
        {
            Id = new UserId(5),
            Name = "AuthUser",
            Email = Email.Parse("auth@test.dev"),
            PasswordHash = "hashedPassword"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act
        User? byName = await sut.GetUserEntityForAuthenticationByNameAsync("AuthUser");
        User? byEmail = await sut.GetUserEntityForAuthenticationByEmailAsync("auth@test.dev");
        User? byId = await sut.GetUserEntityForAuthenticationByIdAsync(new UserId(5));

        // Assert
        Assert.NotNull(byName);
        Assert.NotNull(byEmail);
        Assert.NotNull(byId);
        Assert.Equal("hashedPassword", byName!.PasswordHash);
    }

    /// <summary>
    /// Tests that <see cref="UserRepository.SetUserEmailVerificationChallengeAsync"/> and
    /// <see cref="UserRepository.CompleteUserEmailVerificationAsync"/> update two-factor properties.
    /// </summary>
    [Fact]
    public async Task VerificationWorkflow_Should_UpdateTwoFactorAndEmailVerifiedFlags()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        UserRepository sut = new(ctx);

        User user = new()
        {
            Id = new UserId(7),
            Name = "VerifyMe",
            Email = Email.Parse("verify@test.dev"),
            PasswordHash = "hash"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // Act 1: Set verification challenge
        DateTime expires = DateTime.UtcNow.AddMinutes(15);
        await sut.SetUserEmailVerificationChallengeAsync(new UserId(7), "123456", expires);

        User? challenged = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == new UserId(7));
        Assert.NotNull(challenged);
        Assert.Equal("123456", challenged!.TwoFactorCode);
        Assert.NotNull(challenged.TwoFactorExpiry);

        // Act 2: Complete verification
        await sut.CompleteUserEmailVerificationAsync(new UserId(7));

        User? completed = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == new UserId(7));
        Assert.NotNull(completed);
        Assert.True(completed!.IsEmailVerified);
        Assert.Null(completed.TwoFactorCode);
        Assert.Null(completed.TwoFactorExpiry);
    }
}
