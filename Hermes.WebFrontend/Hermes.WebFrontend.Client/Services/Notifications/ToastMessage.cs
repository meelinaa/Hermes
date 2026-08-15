namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Represents an individual toast notification message item.
/// </summary>
public sealed class ToastMessage
{
    /// <summary>Gets the unique identifier of the toast message.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Gets the main notification text content.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the optional title header text.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the severity level of the toast.</summary>
    public ToastNotificationLevel Level { get; init; } = ToastNotificationLevel.Info;

    /// <summary>Gets the timestamp when the toast was posted.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Gets the duration in milliseconds before the toast automatically dismisses.</summary>
    public int DurationMs { get; init; } = 4000;
}
