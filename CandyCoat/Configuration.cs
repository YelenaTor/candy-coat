using System.Collections.Generic;
using CandyCoat.Data;

namespace CandyCoat;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsSetupComplete { get; set; } = false;
    public List<CandyCoat.Data.Booking> Bookings { get; set; } = new();
    public List<Patron> Patrons { get; set; } = new();
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public bool EnableGlamourer { get; set; } = true;
    public bool EnableChatTwo { get; set; } = true;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public List<Booking> Bookings { get; set; } = new();
    public List<Patron> Patrons { get; set; } = new();

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
