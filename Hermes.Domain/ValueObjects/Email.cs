using Hermes.Domain.Exceptions;

namespace Hermes.Domain.ValueObjects;

public readonly record struct Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

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

    public static implicit operator string(Email email) => email.Value;
    public static implicit operator Email(string value) => Parse(value);

    public override string ToString() => Value;
}
