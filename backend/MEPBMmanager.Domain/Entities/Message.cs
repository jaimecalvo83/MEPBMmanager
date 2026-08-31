namespace MEPBMmanager.Domain.Entities;

public class Message
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Game Game { get; set; } = null!;
    public Nation Sender { get; set; } = null!;
}
