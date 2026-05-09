using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Notifications.Receiving.DTOs;
using Hermes.Notifications.Receiving.MailHog;
using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving;

/// <summary>
/// Retrieves messages from MailHog using its REST API (<c>GET /api/v2/messages</c>).
/// </summary>
public sealed class MailHogEmailReceiver : IEmailReceiver, IDisposable
{
    private const int PAGE_SIZE = 250;

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly MailHogEnvelopeReader _envelopeReader;
    private readonly MailHogMessageMapper _messageMapper;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="MailHogEmailReceiver"/>.
    /// </summary>
    /// <param name="settings">MailHog API base URL.</param>
    public MailHogEmailReceiver(MailHogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        //MailHogApiUriHelper uriHelper = new();

        _httpClient = new()
        {
            BaseAddress = MailHogApiUriHelper.CreateBaseUri(settings),
        };

        _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        _envelopeReader = new();
        _messageMapper = new();
    }

    /// <inheritdoc />
    public async Task<EmailResult> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "api/v2/messages?start=0&limit=1",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        MailHogMessagesEnvelope? envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelope>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MailHogMessageDto> items = _envelopeReader.GetMessages(envelope);
        if (items.Count == 0)
        {
            throw new InvalidOperationException("No messages are available in MailHog.");
        }

        return _messageMapper.MapToEmailResult(items[0]);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmailResult>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<EmailResult> results = [];
        int start = 0;

        while (true)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                FormattableString.Invariant($"api/v2/messages?start={start}&limit={PAGE_SIZE}"),
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            MailHogMessagesEnvelope? envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelope>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<MailHogMessageDto> items = _envelopeReader.GetMessages(envelope);
            if (items.Count == 0)
            {
                break;
            }

            foreach (MailHogMessageDto item in items)
            {
                results.Add(_messageMapper.MapToEmailResult(item));
            }

            if (items.Count < PAGE_SIZE)
            {
                break;
            }

            start += PAGE_SIZE;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmailResult>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<EmailResult> all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(emailResult =>
            emailResult.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
