using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Notifications.Receiving.DTOs;
using Hermes.Notifications.Receiving.MailHog;
using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving;

public sealed class MailHogEmailReceiver : IEmailReceiver, IDisposable
{
    private const int PAGE_SIZE = 250;

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly MailHogEnvelopeReader _envelopeReader;
    private readonly MailHogMessageMapper _messageMapper;
    private bool _disposed;

    public MailHogEmailReceiver(MailHogOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

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

    public async Task<EmailResultDto> GetLatestAsync(CancellationToken cancellationToken = default)
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

    public async Task<IEnumerable<EmailResultDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<EmailResultDto> results = [];
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

    public async Task<IEnumerable<EmailResultDto>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<EmailResultDto> all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(emailResult =>
            emailResult.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)).ToList();
    }

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
