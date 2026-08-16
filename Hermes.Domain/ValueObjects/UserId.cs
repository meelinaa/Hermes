using Hermes.Domain.Exceptions;

namespace Hermes.Domain.ValueObjects;

/// <summary>
/// Strongly typed domain value object representing a unique user identifier.
/// Provides comparison operators, parsing validation via <see cref="Parse"/>, and JSON serialization support.
/// </summary>
public readonly record struct UserId(int Value) : IComparable<UserId>
{
    /// <summary>
    /// Parses and strictly validates a positive integer into a <see cref="UserId"/>.
    /// </summary>
    /// <param name="value">The positive integer identifier.</param>
    /// <returns>A validated <see cref="UserId"/> instance.</returns>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="value"/> is zero or negative.</exception>
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
