namespace Hermes.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access or modify a newsletter subscription they do not own.
/// Maps to HTTP 403 at the API boundary.
/// </summary>
public sealed class NewsletterSubscriptionAccessDeniedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionAccessDeniedException"/> class with a default message.
    /// </summary>
    public NewsletterSubscriptionAccessDeniedException()
        : base("You do not have permission to access this newsletter subscription entry.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionAccessDeniedException"/> class with a custom message.
    /// </summary>
    /// <param name="message">The custom exception message.</param>
    public NewsletterSubscriptionAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterSubscriptionAccessDeniedException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The custom exception message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public NewsletterSubscriptionAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
