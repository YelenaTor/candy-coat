using System;
using System.Collections.Generic;

namespace CandyCoat.Data;

public class GambaGamePreset
{
    public string Name { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string AnnounceMacro { get; set; } = string.Empty;
    public float DefaultMultiplier { get; set; } = 2.0f;
}

public class GambaPlayer
{
    public string Name { get; set; } = string.Empty;
    public int Bet { get; set; }
}

public class GambaRollEntry
{
    public string PlayerName { get; set; } = string.Empty;
    public int Roll { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
