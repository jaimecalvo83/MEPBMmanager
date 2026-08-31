namespace MEPBMmanager.Domain.Entities;

public class Navy
{
    public string Id { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public int Warships { get; set; } = 0;
    public int Transports { get; set; } = 0;
    public string LocationHex { get; set; } = string.Empty;
    public string? CommanderId { get; set; }
    public int Strength { get; set; } = 0;
    
    public Nation Nation { get; set; } = null!;
}
