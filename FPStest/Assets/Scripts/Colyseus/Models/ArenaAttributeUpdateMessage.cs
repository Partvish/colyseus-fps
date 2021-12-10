using System.Collections.Generic;

public class ArenaAttributeUpdateMessage
{
    public string entityId;
    public string userId;

    public Dictionary<string, string> attributesToSet = new Dictionary<string, string>();
}