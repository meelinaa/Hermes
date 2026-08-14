using System;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Hermes.Application.Logging;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Hermes.Application.Services.Users;

public sealed class VerificationDigestLoggingDecorator : IVerificationDigestService
{
    private readonly IVerificationDigestService _inner;
    private readonly ILogger<VerificationDigestLoggingDecorator> _logger;

    public VerificationDigestLoggingDecorator(
        IVerificationDigestService inner,
        ILogger<VerificationDigestLoggingDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Result<bool>> SendAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _inner.SendAsync(userId, cancellationToken).ConfigureAwait(false);

            if (result.IsFailed)
            {
                _logger.LogError("Verification digest failed for user {UserId}: {Error}", userId.Value, result.Errors.FirstOrDefault()?.Message);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogVerificationCanceled(userId.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogVerificationFailed(ex, userId.Value);
            throw;
        }
    }
}
