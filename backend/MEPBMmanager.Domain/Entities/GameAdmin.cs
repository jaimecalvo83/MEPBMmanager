namespace MEPBMmanager.Domain.Entities;

public class GameAdmin
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsReady { get; set; } = false;
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Game Game { get; set; } = null!;
    public User User { get; set; } = null!;
}
