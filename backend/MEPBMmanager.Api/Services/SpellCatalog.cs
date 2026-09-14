using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Enums;

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
    public int MinCastingRank { get; set; } = 0;
}

public static class SpellCatalog
{
    // Lista oficial de hechizos (Domain.SpellDefinitions): única fuente de verdad.
    // Lost list: 244/246/248 (ofensivos) + 502/504/508/510/512 (conjuring).
    // 314 Teleport es normal (rango 80), no perdido.
    private static readonly HashSet<int> LostIds = new() { 244, 246, 248, 502, 504, 508, 510, 512 };

    private static SpellType MapType(SpellCategory category) => category switch
    {
        SpellCategory.Healing => SpellType.Heal,
        SpellCategory.Defensive => SpellType.Combat,
        SpellCategory.Offensive => SpellType.Combat,
        SpellCategory.Movement => SpellType.Movement,
        SpellCategory.Lore => SpellType.Lore,
        SpellCategory.Conjuring => SpellType.Conjuring,
        _ => SpellType.Enchant
    };

    public static readonly List<SpellDefinition> All = SpellDefinitions.Spells
        .Select(s => new SpellDefinition
        {
            Id = s.Id,
            Name = s.Name,
            College = s.Category.ToString(),
            Type = MapType(s.Category),
            IsLost = LostIds.Contains(s.Id),
            MinCastingRank = s.MinCastingRank
        })
        .ToList();

    public static SpellDefinition? Get(int spellId) => All.FirstOrDefault(s => s.Id == spellId);

    public static List<SpellDefinition> OfType(SpellType type) => All.Where(s => s.Type == type).ToList();
}
