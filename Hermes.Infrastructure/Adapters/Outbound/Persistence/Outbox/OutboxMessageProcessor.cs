using System.Text.Json;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Events;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Outbox;

/// <summary>
/// Service implementation that polls and processes pending transactional outbox messages.
/// Ensures at-least-once delivery of domain events by publishing them to registered handlers.
/// </summary>
public sealed class OutboxMessageProcessor(
    HermesDbContext db,
    IDomainEventDispatcher dispatcher,
    ILogger<OutboxMessageProcessor> logger) : IOutboxMessageProcessor
{
    /// <summary>
    /// Reads unhandled outbox records from the database, deserializes the domain event payload,
    /// and invokes the domain event dispatcher. Marks records as processed upon success or records error details upon failure.
    /// </summary>
    /// <param name="batchSize">The maximum number of records to process per batch execution.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The number of outbox messages successfully dispatched in this invocation.</returns>
    public async Task<int> ProcessPendingMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        List<OutboxMessage> pendingMessages = await db.OutboxMessages
            .Where(msg => msg.ProcessedAtUtc == null && msg.RetryCount < 5)
            .OrderBy(msg => msg.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pendingMessages.Count == 0)
            return 0;

        int processedCount = 0;
        DateTime nowUtc = DateTime.UtcNow;

        foreach (OutboxMessage message in pendingMessages)
        {
            try
            {
                Type? eventType = ResolveEventType(message.Type);
                if (eventType is null)
                {
                    logger.LogError("Could not resolve domain event type '{TypeName}' for outbox message {MessageId}", message.Type, message.Id);
                    message.MarkFailed($"Could not resolve type '{message.Type}'");
                    continue;
                }

                object? deserialized = JsonSerializer.Deserialize(message.Content, eventType);
                if (deserialized is not IDomainEvent domainEvent)
                {
                    logger.LogError("Failed to deserialize outbox message {MessageId} into IDomainEvent", message.Id);
                    message.MarkFailed("Deserialization did not produce an IDomainEvent instance");
                    continue;
                }

                await dispatcher.DispatchAsync(domainEvent, cancellationToken).ConfigureAwait(false);
                message.MarkProcessed(nowUtc);
                processedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox message {MessageId} for event '{TypeName}'", message.Id, message.Type);
                message.MarkFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processedCount;
    }

    /// <summary>
    /// Resolves the CLR type of a domain event from its type name string across all loaded assemblies.
    /// </summary>
    /// <param name="typeName">The assembly-qualified name, full name, or simple name of the domain event.</param>
    /// <returns>The resolved <see cref="Type"/> instance, or null if resolution fails.</returns>
    private static Type? ResolveEventType(string typeName)
    {
        Type? type = Type.GetType(typeName);
        if (type != null)
            return type;

        // Fallback search across AppDomain assemblies (e.g. Hermes.Domain)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
                return type;

            type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
            if (type != null)
                return type;
        }

        return null;
    }
}
