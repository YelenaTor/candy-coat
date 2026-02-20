using System.Collections.Generic;
using CandyCoat.Data;

namespace CandyCoat.Services;

public class WaitlistManager
{
    public List<WaitlistEntry> Entries { get; private set; } = new();
    
    public void AddToQueue(string name)
    {
        Entries.Add(new WaitlistEntry { PatronName = name });
    }
    
    public void RemoveFromQueue(WaitlistEntry entry)
    {
        Entries.Remove(entry);
    }
    
    public void ClearQueue()
    {
        Entries.Clear();
    }
}
