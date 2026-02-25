using System;

namespace CandyCoat.Data;

public enum ServiceCategory
{
    Session,
    Drink,
    Game,
    Performance,
    Other
}

public class ServiceMenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public ServiceCategory Category { get; set; } = ServiceCategory.Other;
}
