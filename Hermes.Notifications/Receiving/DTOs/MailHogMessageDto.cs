namespace Hermes.Notifications.Receiving.DTOs
{
    internal sealed class MailHogMessageDto
    {
        public string? Id { get; init; }

        public MailHogPathDto? From { get; init; }

        public List<MailHogPathDto>? To { get; init; }

        public MailHogContentDto? Content { get; init; }

        public string? Created { get; init; }
    }
}
