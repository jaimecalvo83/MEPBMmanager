namespace MEPBMmanager.Domain.Entities;

public class Guard
{
    public string Id { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    
    public Character Character { get; set; } = null!;
}
