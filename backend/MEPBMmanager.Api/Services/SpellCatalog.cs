namespace MEPBMmanager.Api.Services;

public enum SpellType
{
    Heal,
    Conjuring,
    Movement,
    Combat,
    Lore,
    Enchant
}

public class SpellDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string College { get; set; } = string.Empty;
    public SpellType Type { get; set; }
}

public static class SpellCatalog
{
    public static readonly List<SpellDefinition> All = new()
    {
        // HEAL (Earth)
        new() { Id = 1, Name = "Heal Wounds", College = "Earth", Type = SpellType.Heal },
        new() { Id = 2, Name = "Cure Disease", College = "Earth", Type = SpellType.Heal },

        // CONJURING (Astral)
        new() { Id = 3, Name = "Conjure Gold", College = "Astral", Type = SpellType.Conjuring },

        // MOVEMENT (Air)
        new() { Id = 4, Name = "Teleport", College = "Air", Type = SpellType.Movement },
        new() { Id = 5, Name = "Haste", College = "Air", Type = SpellType.Movement },

        // COMBAT
        new() { Id = 6, Name = "Fireball", College = "Fire", Type = SpellType.Combat },
        new() { Id = 7, Name = "Lightning Bolt", College = "Air", Type = SpellType.Combat },
        new() { Id = 8, Name = "Curse", College = "Death", Type = SpellType.Combat },
        new() { Id = 9, Name = "Protection", College = "Earth", Type = SpellType.Combat },

        // LORE
        new() { Id = 10, Name = "Scry", College = "Water", Type = SpellType.Lore },
        new() { Id = 11, Name = "Probe", College = "Astral", Type = SpellType.Lore },

        // ENCHANT
        new() { Id = 12, Name = "Enchant Weapon", College = "Earth", Type = SpellType.Enchant },
        new() { Id = 13, Name = "Bless Army", College = "Astral", Type = SpellType.Enchant }
    };

    public static SpellDefinition? Get(int spellId) => All.FirstOrDefault(s => s.Id == spellId);

    public static List<SpellDefinition> OfType(SpellType type) => All.Where(s => s.Type == type).ToList();
}
