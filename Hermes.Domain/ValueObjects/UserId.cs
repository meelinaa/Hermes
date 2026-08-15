using Hermes.Domain.Exceptions;

namespace Hermes.Domain.ValueObjects;

/// <summary>
/// Strongly typed value object representing a unique user identifier.
/// </summary>
public readonly record struct UserId(int Value) : IComparable<UserId>
{
    /// <summary>Parses and validates a positive integer into a UserId.</summary>
    public static UserId Parse(int value)
    {
        if (value <= 0)
            throw new DomainValidationException("UserId must be positive.");
        
        return new UserId(value);
    }

    /// <inheritdoc />
    public int CompareTo(UserId other) => Value.CompareTo(other.Value);

    /// <summary>Determines if left operand is less than right operand.</summary>
    public static bool operator <(UserId left, UserId right) => left.Value < right.Value;

    /// <summary>Determines if left operand is less than or equal to right operand.</summary>
    public static bool operator <=(UserId left, UserId right) => left.Value <= right.Value;

    /// <summary>Determines if left operand is greater than right operand.</summary>
    public static bool operator >(UserId left, UserId right) => left.Value > right.Value;

    /// <summary>Determines if left operand is greater than or equal to right operand.</summary>
    public static bool operator >=(UserId left, UserId right) => left.Value >= right.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
