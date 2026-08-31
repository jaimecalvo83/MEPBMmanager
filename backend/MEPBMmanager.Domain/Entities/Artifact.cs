namespace MEPBMmanager.Domain.Entities;

public class Artifact
{
    public string Id { get; set; } = string.Empty;
    public string? NationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Alignment { get; set; } = "none";
    public int Bonus { get; set; } = 0;
    public string? LocationHex { get; set; }
    public string? HeldByCharacterId { get; set; }
    public Character? HeldByCharacter { get; set; }
    public bool IsAtCapital { get; set; } = true;
}
