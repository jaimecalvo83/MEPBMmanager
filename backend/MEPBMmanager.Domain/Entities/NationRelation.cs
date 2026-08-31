namespace MEPBMmanager.Domain.Entities;

public class NationRelation
{
    public string Id { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string TargetNationId { get; set; } = string.Empty;
    public int Level { get; set; } = 0;
    
    public Nation Nation { get; set; } = null!;
    public Nation TargetNation { get; set; } = null!;
}
