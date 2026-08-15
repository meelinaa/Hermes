namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Defines operations for polling, deserializing, and dispatching pending domain events saved in the transactional outbox table.
/// </summary>
public interface IOutboxMessageProcessor
{
    /// <summary>
    /// Fetches unhandled outbox messages in creation order, publishes them to their respective domain event handlers,
    /// and updates their status to processed or records failure state.
    /// This method is invoked by scheduled workers or immediately after state persistence.
    /// </summary>
    /// <param name="batchSize">The maximum number of outbox messages to process in a single batch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The total number of successfully processed messages in this batch.</returns>
    Task<int> ProcessPendingMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default);
}
