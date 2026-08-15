namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Represents the result of an API client operation with optional error and problem details metadata.
/// </summary>
public sealed record ApiResult
{
    /// <summary>Gets whether the API request was successful.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the user-facing error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the RFC 7807 problem type URI if returned by the API.</summary>
    public string? ProblemType { get; init; }

    /// <summary>Gets the HTTP status code returned by the server.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Gets validation error details by field name.</summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>Creates a successful API result.</summary>
    public static ApiResult Success() => new() { IsSuccess = true };

    /// <summary>Creates a failed API result with error metadata.</summary>
    public static ApiResult Failure(
        string errorMessage,
        string? problemType = null,
        int? statusCode = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ProblemType = problemType,
            StatusCode = statusCode,
            ValidationErrors = validationErrors
        };
}

/// <summary>
/// Represents the strongly typed data result of an API client operation.
/// </summary>
/// <typeparam name="T">The data payload type returned on success.</typeparam>
public sealed record ApiResult<T>
{
    /// <summary>Gets whether the API request was successful.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the returned data payload when successful.</summary>
    public T? Value { get; init; }

    /// <summary>Gets the user-facing error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the RFC 7807 problem type URI if returned by the API.</summary>
    public string? ProblemType { get; init; }

    /// <summary>Gets the HTTP status code returned by the server.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Gets validation error details by field name.</summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>Creates a successful API result with data.</summary>
    public static ApiResult<T> Success(T value) => new() { IsSuccess = true, Value = value };

    /// <summary>Creates a failed API result with error metadata.</summary>
    public static ApiResult<T> Failure(
        string errorMessage,
        string? problemType = null,
        int? statusCode = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ProblemType = problemType,
            StatusCode = statusCode,
            ValidationErrors = validationErrors
        };
}
