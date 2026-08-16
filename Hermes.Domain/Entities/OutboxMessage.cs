namespace Hermes.Domain.Entities;

/// <summary>
/// Represents a domain event serialized and persisted atomically in the database to guarantee at-least-once asynchronous dispatch.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Gets the unique identifier of the outbox message.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the CLR type name or event descriptor for deserialization.
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the JSON-serialized payload of the domain event.
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the message was saved to the outbox table.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the timestamp when the message was successfully dispatched and processed.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Gets the error details if the message processing failed during execution.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets the number of processing attempts for this message.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Private parameterless constructor required by EF Core.
    /// </summary>
    private OutboxMessage() { }

    /// <summary>
    /// Creates a new outbox message entity to store an unhandled domain event.
    /// Used by the persistence layer within the same transaction as entity changes to prevent message loss.
    /// </summary>
    /// <param name="type">The fully qualified or recognizable type name of the event.</param>
    /// <param name="content">The serialized JSON representation of the domain event.</param>
    /// <param name="createdAtUtc">The creation timestamp in UTC.</param>
    /// <returns>A new <see cref="OutboxMessage"/> instance initialized for processing.</returns>
    public static OutboxMessage Create(string type, string content, DateTime createdAtUtc)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Content = content,
            CreatedAtUtc = createdAtUtc,
            ProcessedAtUtc = null,
            Error = null,
            RetryCount = 0
        };
    }

    /// <summary>
    /// Marks the message as successfully dispatched and processed, preventing subsequent executions.
    /// </summary>
    /// <param name="processedAtUtc">The timestamp when the event handling finished successfully.</param>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    /// <summary>
    /// Increments the retry counter and records error information when event processing fails.
    /// Enables transient retry strategies and diagnostic investigation of dead-letter messages.
    /// </summary>
    /// <param name="error">The error message or exception details.</param>
    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;
    }
}
