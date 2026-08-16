using Hermes.Domain.Exceptions;

namespace Hermes.Domain.ValueObjects;

/// <summary>
/// Strongly typed domain value object representing a validated, normalized (lowercase, trimmed) email address.
/// Guards against null/empty default struct state and invalid email structures.
/// </summary>
public readonly record struct Email
{
    private readonly string? _value;

    /// <summary>
    /// Gets the normalized string value of the email address, or an empty string if in uninitialized default state.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the email instance is uninitialized or empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>
    /// Private constructor enforcing instantiation exclusively through <see cref="Parse"/>.
    /// </summary>
    /// <param name="value">The normalized email string.</param>
    private Email(string value) => _value = value;

    /// <summary>
    /// Parses, trims, normalizes, and validates an input string into a valid <see cref="Email"/> value object.
    /// </summary>
    /// <param name="input">The raw email string to parse.</param>
    /// <returns>A validated <see cref="Email"/> instance in lowercase.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
    /// <exception cref="InvalidEmailException">Thrown when <paramref name="input"/> is empty, exceeds 254 chars, or is malformed.</exception>
    public static Email Parse(string? input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string trimmed = input.Trim();
        if (trimmed.Length == 0)
            throw new InvalidEmailException("E-mail cannot be empty.");

        string v = trimmed.ToLowerInvariant();
        if (v.Length > 254)
            throw new InvalidEmailException("E-mail is too long.");

        int at = v.IndexOf('@');
        if (at <= 0 || at == v.Length - 1 || v.AsSpan(at + 1).IndexOf('@') >= 0)
            throw new InvalidEmailException("Invalid e-mail format.");

        return new Email(v);
    }

    /// <summary>Implicit conversion from Email value object to its underlying string value.</summary>
    public static implicit operator string(Email email) => email.Value;

    /// <summary>Implicit conversion from a string to an Email value object via <see cref="Parse"/>.</summary>
    public static implicit operator Email(string value) => Parse(value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
