namespace MEPBMmanager.Domain.Entities;

public class Game
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GameTypeId { get; set; }
    public string Status { get; set; } = "setup";
    public int CurrentTurn { get; set; } = 0;
    public int MaxTurns { get; set; } = 200;
    public int TurnIntervalDays { get; set; } = 14;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    
    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
    public ICollection<Nation> Nations { get; set; } = new List<Nation>();
    public ICollection<HexTile> HexTiles { get; set; } = new List<HexTile>();
    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<GameEvent> Events { get; set; } = new List<GameEvent>();
    public ICollection<MarketPrice> MarketPrices { get; set; } = new List<MarketPrice>();
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public GameType GameType { get; set; } = null!;
}
