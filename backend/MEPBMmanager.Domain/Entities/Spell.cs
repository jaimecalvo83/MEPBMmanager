namespace MEPBMmanager.Domain.Entities;

public class Spell
{
    public string Id { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public int SpellId { get; set; }
    public bool IsKnown { get; set; } = true;
    public bool IsLost { get; set; } = false;
    
    public Character Character { get; set; } = null!;
}
