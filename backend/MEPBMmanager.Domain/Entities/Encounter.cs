namespace MEPBMmanager.Domain.Entities;

// Encuentro aleatorio generado durante el movimiento (reglamento: capítulo de Encuentros).
// Un personaje en el hex puede reaccionar (orden 285) o investigarlo (orden 290).
public class Encounter
{
    public string Id { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string? LocationHex { get; set; }

    public string? CharacterId { get; set; }
    public string? ArmyId { get; set; }

    public string Type { get; set; } = "creature"; // creature, npc, character, artifact, lore
    public string Description { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
    public string? Result { get; set; }

    public Game Game { get; set; } = null!;
    public Character? Character { get; set; }
    public Army? Army { get; set; }
}
