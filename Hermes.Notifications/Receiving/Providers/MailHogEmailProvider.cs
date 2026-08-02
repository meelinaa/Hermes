using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Notifications.Receiving.DTOs;
using Hermes.Notifications.Receiving.Interfaces;
using Hermes.Notifications.Receiving.MailHog;
using Hermes.Notifications.Receiving.MailHog.Mappers;
using Hermes.Notifications.Receiving.Options;

namespace Hermes.Notifications.Receiving.Providers;

/// <summary>
/// MailHog email provider for inspecting and retrieving dev emails via the MailHog HTTP REST API.
/// </summary>
public sealed class MailHogEmailProvider : IEmailProvider, IDisposable
{
    private const int PAGE_SIZE = 250;

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly MailHogEnvelopeProvider _envelopeReader;
    private readonly MailHogMessageMapper _messageMapper;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailHogEmailProvider"/> class with the specified MailHog options.
    /// </summary>
    /// <param name="settings">The MailHog configuration settings.</param>
    public MailHogEmailProvider(MailHogOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _httpClient = new()
        {
            BaseAddress = MailHogApiUriFactory.CreateBaseUri(settings),
        };

        _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        _envelopeReader = new();
        _messageMapper = new();
    }

    /// <summary>
    /// Retrieves the most recently received email from MailHog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest email result DTO.</returns>
    public async Task<EmailResultDto> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "api/v2/messages?start=0&limit=1",
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        MailHogMessagesEnvelopeDto? envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelopeDto>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MailHogMessageDto> items = _envelopeReader.GetMessages(envelope);
        if (items.Count == 0)
        {
            throw new InvalidOperationException("No messages are available in MailHog.");
        }

        return _messageMapper.MapToEmailResult(items[0]);
    }

    /// <summary>
    /// Retrieves all received emails from MailHog using paginated HTTP requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of email result DTOs.</returns>
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

            MailHogMessagesEnvelopeDto? envelope = await response.Content.ReadFromJsonAsync<MailHogMessagesEnvelopeDto>(_jsonOptions, cancellationToken)
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

    /// <summary>
    /// Retrieves emails whose subject line contains the specified substring.
    /// </summary>
    /// <param name="subject">The subject substring to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching email result DTOs.</returns>
    public async Task<IEnumerable<EmailResultDto>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        IEnumerable<EmailResultDto> all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(emailResult =>
            emailResult.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Disposes the underlying HTTP client.
    /// </summary>
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
