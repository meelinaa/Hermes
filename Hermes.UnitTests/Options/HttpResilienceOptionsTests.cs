using System.ComponentModel.DataAnnotations;
using Hermes.Application.Options.External;
using Xunit;

namespace Hermes.UnitTests.ApplicationOptions;

public sealed class HttpResilienceOptionsTests
{
    [Fact]
    public void Defaults_Should_Have_Sensible_Values()
    {
        var options = new HttpResilienceOptions();

        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(1000, options.BaseDelayMilliseconds);
        Assert.Equal(10, options.AttemptTimeoutSeconds);
        Assert.Equal(30, options.TotalRequestTimeoutSeconds);
        Assert.Equal(30, options.CircuitBreakerSamplingDurationSeconds);
        Assert.Equal(0.5, options.CircuitBreakerFailureRatio);
        Assert.Equal(5, options.CircuitBreakerMinimumThroughput);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(11, false)]
    public void MaxRetryAttempts_Validation(int attempts, bool isValid)
    {
        var options = new HttpResilienceOptions { MaxRetryAttempts = attempts };
        var results = new List<ValidationResult>();
        var context = new ValidationContext(options) { MemberName = nameof(HttpResilienceOptions.MaxRetryAttempts) };

        bool valid = Validator.TryValidateProperty(options.MaxRetryAttempts, context, results);
        Assert.Equal(isValid, valid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, true)]
    [InlineData(61, false)]
    public void AttemptTimeoutSeconds_Validation(int timeout, bool isValid)
    {
        var options = new HttpResilienceOptions { AttemptTimeoutSeconds = timeout };
        var results = new List<ValidationResult>();
        var context = new ValidationContext(options) { MemberName = nameof(HttpResilienceOptions.AttemptTimeoutSeconds) };

        bool valid = Validator.TryValidateProperty(options.AttemptTimeoutSeconds, context, results);
        Assert.Equal(isValid, valid);
    }
}
