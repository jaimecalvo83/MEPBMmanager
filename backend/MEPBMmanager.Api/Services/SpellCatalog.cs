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
    public bool IsLost { get; set; } = false;
}

public static class SpellCatalog
{
    public static readonly List<SpellDefinition> All = new()
    {
        // HEAL (Earth)
        new() { Id = 1, Name = "Heal Wounds", College = "Earth", Type = SpellType.Heal },
        new() { Id = 2, Name = "Minor Heal", College = "Earth", Type = SpellType.Heal },

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
        new() { Id = 13, Name = "Bless Army", College = "Astral", Type = SpellType.Enchant },

        // BÁSICOS oficiales vistos en turnos 0 (no perdidos; el 2 corrige nombre)
        new() { Id = 302, Name = "Long Stride", College = "Air", Type = SpellType.Movement },
        new() { Id = 304, Name = "Fast Stride", College = "Air", Type = SpellType.Movement },

        // LOST (solo investigables con acceso nacional: LOST_SPELL_<id>)
        new() { Id = 244, Name = "Fearful Hearts", College = "Death", Type = SpellType.Combat, IsLost = true },
        new() { Id = 246, Name = "Summon Storms", College = "Death", Type = SpellType.Combat, IsLost = true },
        new() { Id = 248, Name = "Fanaticism", College = "Death", Type = SpellType.Combat, IsLost = true },
        new() { Id = 314, Name = "Teleport (Lost)", College = "Air", Type = SpellType.Movement, IsLost = true },
        new() { Id = 508, Name = "Conjure Mounts", College = "Astral", Type = SpellType.Conjuring, IsLost = true },
        new() { Id = 512, Name = "Conjure Hordes", College = "Astral", Type = SpellType.Conjuring, IsLost = true }
    };

    public static SpellDefinition? Get(int spellId) => All.FirstOrDefault(s => s.Id == spellId);

    public static List<SpellDefinition> OfType(SpellType type) => All.Where(s => s.Type == type).ToList();
}
