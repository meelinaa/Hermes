using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.External;

/// <summary>
/// Configuration options for standard HTTP client resilience pipelines (Polly v8).
/// Controls timeouts, exponential backoff retries, and circuit breaker heuristics for external API integrations.
/// </summary>
public sealed class HttpResilienceOptions
{
    /// <summary>
    /// Configuration section key name in appsettings.json.
    /// </summary>
    public const string SECTION_NAME = "HttpResilience";

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient HTTP failures.
    /// </summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base backoff delay in milliseconds before the first retry attempt.
    /// </summary>
    [Range(100, 10000)]
    public int BaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the individual HTTP request attempt timeout in seconds.
    /// </summary>
    [Range(1, 60)]
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the total request timeout in seconds encompassing all retry attempts.
    /// </summary>
    [Range(5, 120)]
    public int TotalRequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the sampling duration in seconds for the circuit breaker evaluation window.
    /// </summary>
    [Range(5, 300)]
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the failure ratio threshold (0.0 to 1.0) required to trip the circuit breaker.
    /// </summary>
    [Range(0.1, 1.0)]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the minimum throughput of requests required before the circuit breaker evaluates failure ratio.
    /// </summary>
    [Range(2, 100)]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;
}
