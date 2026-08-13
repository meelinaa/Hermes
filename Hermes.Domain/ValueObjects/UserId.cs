namespace Hermes.Domain.ValueObjects;

public readonly record struct UserId(int Value) : IComparable<UserId>
{
    public static UserId Parse(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "UserId must be positive.");
        
        return new UserId(value);
    }

    public int CompareTo(UserId other) => Value.CompareTo(other.Value);
    
    public override string ToString() => Value.ToString();
}
