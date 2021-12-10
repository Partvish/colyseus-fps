public class ArenaPropertyCompareResult
{
    public string Name { get; private set; }
    public object OldValue { get; private set; }
    public object NewValue { get; private set; }

    public ArenaPropertyCompareResult(string name, object oldValue, object newValue)
    {
        Name = name;
        OldValue = oldValue;
        NewValue = newValue;
    }
}