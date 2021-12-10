public enum ArenaRFCTargets
{
    ALL,
    OTHERS
}

public class ArenaRFCMessage
{
    public ArenaRFCTargets target = ArenaRFCTargets.ALL;
    public string entityId;
    public string function;
    public object[] param;
}