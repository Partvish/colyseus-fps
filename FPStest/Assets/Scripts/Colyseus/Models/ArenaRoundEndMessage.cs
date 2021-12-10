using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaRoundEndMessage : ArenaMessage
{
    public Winner winner;
}

public class Winner
{
    public string id;
    public int score;
    public bool tie = false;
    public string[] tied;
}