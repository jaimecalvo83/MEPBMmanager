namespace MEPBMmanager.Domain.Entities;

public class Turn
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Status { get; set; } = "pending";
    public string Season { get; set; } = "summer";
    public DateTime Deadline { get; set; }
    public DateTime? ProcessedAt { get; set; }
    
    public Game Game { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<TurnResult> Results { get; set; } = new List<TurnResult>();
    public ICollection<GameEvent> Events { get; set; } = new List<GameEvent>();
}
