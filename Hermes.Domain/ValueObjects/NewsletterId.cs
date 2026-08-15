using Hermes.Domain.Exceptions;

namespace Hermes.Domain.ValueObjects;

/// <summary>
/// Strongly typed value object representing a unique newsletter subscription identifier.
/// </summary>
public readonly record struct NewsletterId(int Value) : IComparable<NewsletterId>
{
    /// <summary>Parses and validates a positive integer into a NewsletterId.</summary>
    public static NewsletterId Parse(int value)
    {
        if (value <= 0)
            throw new DomainValidationException("NewsletterId must be positive.");
        
        return new NewsletterId(value);
    }

    /// <inheritdoc />
    public int CompareTo(NewsletterId other) => Value.CompareTo(other.Value);

    /// <summary>Determines if left operand is less than right operand.</summary>
    public static bool operator <(NewsletterId left, NewsletterId right) => left.Value < right.Value;

    /// <summary>Determines if left operand is less than or equal to right operand.</summary>
    public static bool operator <=(NewsletterId left, NewsletterId right) => left.Value <= right.Value;

    /// <summary>Determines if left operand is greater than right operand.</summary>
    public static bool operator >(NewsletterId left, NewsletterId right) => left.Value > right.Value;

    /// <summary>Determines if left operand is greater than or equal to right operand.</summary>
    public static bool operator >=(NewsletterId left, NewsletterId right) => left.Value >= right.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
