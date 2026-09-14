namespace MEPBMmanager.Domain.Entities;

public class Army
{
    public string Id { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationHex { get; set; } = string.Empty;
    public string? CommanderId { get; set; }
    
    // Troops
    public int HeavyCavalry { get; set; } = 0;
    public int LightCavalry { get; set; } = 0;
    public int HeavyInfantry { get; set; } = 0;
    public int LightInfantry { get; set; } = 0;
    public int Archers { get; set; } = 0;
    public int MenAtArms { get; set; } = 0;
    
    // Equipment per troop type
    public int HCWeaponRank { get; set; } = 10;
    public int HCArmourRank { get; set; } = 0;
    public int LCWeaponRank { get; set; } = 10;
    public int LCArmourRank { get; set; } = 0;
    public int HIWeaponRank { get; set; } = 10;
    public int HIArmourRank { get; set; } = 0;
    public int LIWeaponRank { get; set; } = 10;
    public int LIArmourRank { get; set; } = 0;
    public int ArcherWeaponRank { get; set; } = 10;
    public int ArcherArmourRank { get; set; } = 0;
    public int MAAWeaponRank { get; set; } = 10;
    public int MAAArmourRank { get; set; } = 0;
    
    // Stats
    public int Morale { get; set; } = 30;
    public int Training { get; set; } = 10;
    public int Food { get; set; } = 0;
    public int WarMachines { get; set; } = 0;
    public bool IsOnManoeuvres { get; set; } = false;
    
    public Nation Nation { get; set; } = null!;
    public ICollection<Character> Characters { get; set; } = new List<Character>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
}
