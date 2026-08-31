namespace MEPBMmanager.Domain.Entities;

public class TurnResult
{
    public string Id { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string Content { get; set; } = "{}";
    
    public Turn Turn { get; set; } = null!;
}
