namespace Hermes.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested newsletter subscription entry was not found in the system.
/// Maps to HTTP 404 at the API boundary.
/// </summary>
public sealed class NewsletterSubscriptionNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionNotFoundException"/> class with a default message.
    /// </summary>
    public NewsletterSubscriptionNotFoundException()
        : base("The requested newsletter subscription entry was not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="message">The custom exception message.</param>
    public NewsletterSubscriptionNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionNotFoundException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The custom exception message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public NewsletterSubscriptionNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
