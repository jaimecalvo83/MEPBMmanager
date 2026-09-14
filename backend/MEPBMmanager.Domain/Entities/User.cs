namespace MEPBMmanager.Domain.Entities;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RoleId { get; set; } = "game_user";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public ICollection<Player> Players { get; set; } = new List<Player>();
}
