namespace MEPBMmanager.Domain.Entities;

public class Character
{
    public string Id { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "commander";
    public bool IsChampion { get; set; } = false;
    
    // Skills
    public int CommandSkill { get; set; } = 10;
    public int AgentSkill { get; set; } = 10;
    public int EmissarySkill { get; set; } = 10;
    public int MageSkill { get; set; } = 10;
    
    // Health
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    
    // Stats
    public int Stealth { get; set; } = 0;
    public int ChallengeRank { get; set; } = 0;
    public string LocationHex { get; set; } = string.Empty;
    
    // Status
    public bool IsDead { get; set; } = false;
    public bool IsKidnapped { get; set; } = false;
    public string? HeldByNationId { get; set; }
    public string? CompanyId { get; set; }
    public string? ArmyId { get; set; }
    
    public Nation Nation { get; set; } = null!;
    public Company? Company { get; set; }
    public Army? Army { get; set; }
    public ICollection<Spell> Spells { get; set; } = new List<Spell>();
    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Guard> GuardedBy { get; set; } = new List<Guard>();
    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
}
