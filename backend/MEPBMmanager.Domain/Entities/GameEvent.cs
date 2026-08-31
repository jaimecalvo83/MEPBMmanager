namespace MEPBMmanager.Domain.Entities;

public class GameEvent
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string? TurnId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Data { get; set; } = "{}";
    
    public Game Game { get; set; } = null!;
    public Turn? Turn { get; set; }
}
