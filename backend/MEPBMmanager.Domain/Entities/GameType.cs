namespace MEPBMmanager.Domain.Entities;

public class GameType
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;   // "1650", "2950", "pruebas"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<NationTemplate> Templates { get; set; } = new List<NationTemplate>();
    public ICollection<HexTile> HexTiles { get; set; } = new List<HexTile>();
    public ICollection<Game> Games { get; set; } = new List<Game>();
}
