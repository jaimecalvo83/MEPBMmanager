namespace MEPBMmanager.Domain.Entities;

public class Company
{
    public string Id { get; set; } = string.Empty;
    public string NationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationHex { get; set; } = string.Empty;
    
    public Nation Nation { get; set; } = null!;
    public ICollection<Character> Characters { get; set; } = new List<Character>();
}
