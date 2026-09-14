namespace MEPBMmanager.Domain.Entities;

public class Nation
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Allegiance { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsEliminated { get; set; } = false;
    public DateTime? EliminatedAt { get; set; }
    public string? StartHex { get; set; }
    
    // Resources
    public int Gold { get; set; } = 10000;
    public int Food { get; set; } = 5000;
    public int Timber { get; set; } = 2000;
    public int Leather { get; set; } = 1000;
    public int Bronze { get; set; } = 500;
    public int Steel { get; set; } = 200;
    public int Mithril { get; set; } = 0;
    public int Mounts { get; set; } = 500;
    public int TaxRate { get; set; } = 30;
    public int VictoryPoints { get; set; } = 0;
    public int WarshipStrength { get; set; } = 3;
    
    public Game Game { get; set; } = null!;
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Character> Characters { get; set; } = new List<Character>();
    public ICollection<Army> Armies { get; set; } = new List<Army>();
    public ICollection<Company> Companies { get; set; } = new List<Company>();
    public ICollection<Navy> Navies { get; set; } = new List<Navy>();
    public ICollection<PopulationCentre> PopulationCentres { get; set; } = new List<PopulationCentre>();
    public ICollection<NationRelation> Relations { get; set; } = new List<NationRelation>();
    public ICollection<NationRelation> RelationsTo { get; set; } = new List<NationRelation>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
