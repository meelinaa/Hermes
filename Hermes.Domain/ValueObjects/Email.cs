namespace Hermes.Domain.ValueObjects;

/// <summary>Normalized primary e-mail (trimmed, lower-case).</summary>
public readonly record struct Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    /// <summary>Parses and normalizes <paramref name="input"/>; rejects empty or malformed values.</summary>
    public static Email Parse(string? input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string trimmed = input.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("E-mail cannot be empty.", nameof(input));

        string v = trimmed.ToLowerInvariant();
        if (v.Length > 254)
            throw new ArgumentException("E-mail is too long.", nameof(input));

        int at = v.IndexOf('@');
        if (at <= 0 || at == v.Length - 1 || v.AsSpan(at + 1).IndexOf('@') >= 0)
            throw new ArgumentException("Invalid e-mail format.", nameof(input));

        return new Email(v);
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
