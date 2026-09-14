using System.ComponentModel.DataAnnotations.Schema;

namespace MEPBMmanager.Domain.Entities;

public class HexTile
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public int Q { get; set; }
    public int R { get; set; }
    public string Terrain { get; set; } = "plains";
    public bool HasBridge { get; set; } = false;
    public bool HasFord { get; set; } = false;
    public bool HasMajorRiver { get; set; } = false;
    public bool HasMinorRiver { get; set; } = false;
    public bool HasRoad { get; set; } = false;
    public string? OwnerId { get; set; }
    public string? GameTypeId { get; set; }

    [NotMapped]
    public string LocationHex => $"{Q},{R}";

    public Game Game { get; set; } = null!;
    public GameType GameType { get; set; } = null!;
}
