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
    private const int PageSize = 250;

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

        var uriHelper = new MailHogApiUriHelper();

        _httpClient = new HttpClient
        {
            BaseAddress = uriHelper.CreateBaseUri(settings),
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        _envelopeReader = new MailHogEnvelopeReader();
        _messageMapper = new MailHogMessageMapper();
    }

    /// <inheritdoc />
    public async Task<EmailResult> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            "api/v2/messages?start=0&limit=1",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelope>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var items = _envelopeReader.GetMessages(envelope);
        if (items.Count == 0)
        {
            throw new InvalidOperationException("No messages are available in MailHog.");
        }

        return _messageMapper.MapToEmailResult(items[0]);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmailResult>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<EmailResult>();
        var start = 0;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                FormattableString.Invariant($"api/v2/messages?start={start}&limit={PageSize}"),
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelope>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);

            var items = _envelopeReader.GetMessages(envelope);
            if (items.Count == 0)
            {
                break;
            }

            foreach (var item in items)
            {
                results.Add(_messageMapper.MapToEmailResult(item));
            }

            if (items.Count < PageSize)
            {
                break;
            }

            start += PageSize;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EmailResult>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(m =>
            m.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)).ToList();
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
