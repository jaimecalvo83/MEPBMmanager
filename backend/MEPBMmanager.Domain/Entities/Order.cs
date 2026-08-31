namespace MEPBMmanager.Domain.Entities;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string? ArmyId { get; set; }
    public string? NavyId { get; set; }
    public int Code { get; set; }
    public string Parameters { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public string? Result { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    
    public Game Game { get; set; } = null!;
    public Turn Turn { get; set; } = null!;
    public Nation Nation { get; set; } = null!;
    public Character Character { get; set; } = null!;
    public Army? Army { get; set; }
    public Navy? Navy { get; set; }
}
