namespace MEPBMmanager.Domain.Entities;

public class PopulationCentre
{
    public string Id { get; set; } = string.Empty;
    public string? NationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = "village";
    public string LocationHex { get; set; } = string.Empty;
    public int Loyalty { get; set; } = 50;
    public int Production { get; set; } = 100;
    public int Stores { get; set; } = 0;
    public bool IsCapital { get; set; } = false;
    public bool IsHidden { get; set; } = false;
    public bool IsSieged { get; set; } = false;
    public bool HasHarbour { get; set; } = false;
    public bool HasPort { get; set; } = false;
    public string? Fortification { get; set; }
    
    public Nation? Nation { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
