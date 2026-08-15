using Hermes.Application.Errors;
using Xunit;

namespace Hermes.UnitTests.DomainErrors;

public sealed class DomainErrorsTests
{
    [Fact]
    public void DuplicateEmailError_Should_Contain_Formatted_Message()
    {
        var error = new DuplicateEmailError("user@test.com");
        Assert.Contains("user@test.com", error.Message);
    }

    [Fact]
    public void UserNotFoundError_WithId_Should_Contain_UserId()
    {
        var error = new UserNotFoundError(42);
        Assert.Contains("42", error.Message);
    }

    [Fact]
    public void UserNotFoundError_WithName_Should_Contain_Name()
    {
        var error = new UserNotFoundError("alice", isEmail: false);
        Assert.Contains("alice", error.Message);
    }

    [Fact]
    public void UserNotFoundError_WithEmail_Should_Contain_Email()
    {
        var error = new UserNotFoundError("alice@test.com", isEmail: true);
        Assert.Contains("alice@test.com", error.Message);
    }

    [Fact]
    public void InvalidCurrentPasswordError_Should_Have_StandardMessage()
    {
        var error = new InvalidCurrentPasswordError();
        Assert.Equal("Current password verification failed.", error.Message);
    }

    [Fact]
    public void InvalidCredentialsError_Should_Have_StandardMessage()
    {
        var error = new InvalidCredentialsError();
        Assert.Equal("Invalid login or password.", error.Message);
    }

    [Fact]
    public void TokenCompromisedError_Should_Preserve_Message()
    {
        var error = new TokenCompromisedError("Replay detected");
        Assert.Equal("Replay detected", error.Message);
    }
}
