namespace Hermes.Domain.ValueObjects;

public readonly record struct NewsletterId(int Value) : IComparable<NewsletterId>
{
    public static NewsletterId Parse(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "NewsletterId must be positive.");
        
        return new NewsletterId(value);
    }

    public int CompareTo(NewsletterId other) => Value.CompareTo(other.Value);
    
    public override string ToString() => Value.ToString();
}
