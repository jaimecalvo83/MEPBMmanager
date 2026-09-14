namespace MEPBMmanager.Domain.Entities;

public class Player
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = "player";
    public string? NationId { get; set; }
    public bool IsReady { get; set; } = false;
    public string? WantsToPlayWithUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Game Game { get; set; } = null!;
    public Nation? Nation { get; set; }
    public User? WantsToPlayWith { get; set; }
}
