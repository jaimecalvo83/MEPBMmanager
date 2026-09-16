using System.Text.Json;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;

namespace MEPBMmanager.Api.Orders;

/// <summary>
/// Everything a single order check needs in one place: the resolved estimate
/// context plus typed access to its parameters and the shared world lookups.
/// Replaces the long local-function lists and 5-6 argument chains so each
/// validation/cost rule reads as: <c>CheckX(code, scope)</c>.
/// </summary>
public sealed class EstimateScope
{
    private readonly MepbmDbContext _db;

    public EstimateScope(MepbmDbContext db, EstimateCtx ctx)
    {
        _db = db;
        Ctx = ctx;
    }

    public EstimateCtx Ctx { get; }
    public Game Game => Ctx.Game;
    public Nation Nation => Ctx.Nation;
    public Character Character => Ctx.Ch;
    public string? Lang => Ctx.Lang;
    public string? EffectiveLocation => Ctx.EffLoc;
    public Dictionary<string, JsonElement> Parameters => Ctx.Pars;

    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<Order>? Pending { get; set; }
    public PendingUsage? Used { get; set; }

    // ---- raw parameters ----
    public string? Text(string key)
    {
        if (Parameters.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    public int Number(string key, int def = 0)
    {
        if (Parameters.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        return def;
    }

    public List<string> IdList(string key)
    {
        var ids = new List<string>();
        if (!Parameters.TryGetValue(key, out var el)) return ids;
        void Add(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(e.GetString())) ids.Add(e.GetString()!);
            else if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) ids.Add(n.ToString());
        }
        if (el.ValueKind == JsonValueKind.Array) foreach (var e in el.EnumerateArray()) Add(e);
        else Add(el);
        return ids;
    }

    // ---- world lookups ----
    public Character? FindCharacter(string? id) =>
        string.IsNullOrEmpty(id) ? null : Game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == id);

    public PopulationCentre? PopulationAt(string? hex) => hex == null ? null :
        Game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(p => p.LocationHex == hex);

    public PopulationCentre? OwnPopulationAt(string? hex) => hex == null ? null :
        Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);

    public Army? SourceArmy() => Text("armyId") != null
        ? Nation.Armies.FirstOrDefault(a => a.Id == Text("armyId"))
        : Nation.Armies.FirstOrDefault(a => a.Id == Character.ArmyId);

    public Artifact? FindArtifact(string? id) =>
        id == null ? null : _db.Artifacts.FirstOrDefault(x => x.Id == id);

    public bool ArtifactExists(string? id) =>
        id != null && _db.Artifacts.Any(x => x.Id == id);

    public Company? FindCompany(string? id) =>
        id == null ? null : _db.Companies.Find(id);

    public int CountCompanyMembers(string companyId) =>
        _db.Characters.Count(x => x.CompanyId == companyId && !x.IsDead);

    public bool OwnCompanyExists(string? id) =>
        id != null && _db.Companies.Any(c => c.Id == id && c.NationId == Nation.Id);

    public HexTile? TileAt(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r)) return null;
        return _db.HexTiles.FirstOrDefault(h => h.GameId == Game.Id && h.Q == q && h.R == r);
    }

    // ---- rule helpers ----
    public string? CapitalHex => Nation.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
    public bool AtCapital => CapitalHex != null && CapitalHex == EffectiveLocation;

    public bool IsSameOrFriendlyNation(string a, string b) => a == b ||
        (Game.Nations.FirstOrDefault(x => x.Id == a)?.Relations.FirstOrDefault(r => r.TargetNationId == b)?.Level ?? 0) >= 1;

    public bool HasEnemyForces(string? hex)
    {
        if (hex == null) return false;
        bool Hostile(string a, string b) => Game.Nations.FirstOrDefault(x => x.Id == a)?.Relations
            .FirstOrDefault(r => r.TargetNationId == b)?.Level <= -1;
        return Game.Nations
            .SelectMany(x => x.Armies.Select(a => new { a.LocationHex, NationId = x.Id })
                .Concat(x.Navies.Select(v => new { v.LocationHex, NationId = x.Id })))
            .Any(u => u.LocationHex == hex && u.NationId != Nation.Id
                && (Hostile(u.NationId, Nation.Id) || Hostile(Nation.Id, u.NationId)));
    }

    public bool IsLandHex(string? hex)
    {
        var tile = TileAt(hex);
        return tile == null || (tile.Terrain != "water" && tile.Terrain != "ocean");
    }

    public bool ArtifactUsableByNation(Artifact a)
    {
        var alignment = (a.Alignment ?? "").ToLower();
        if (alignment is "" or "none" or "neutral") return true;
        if (alignment == "good") return Nation.Allegiance == "free_peoples";
        if (alignment == "evil") return Nation.Allegiance == "dark_servants";
        return true;
    }

    public bool CommandsForce(Character c) => c.ArmyId != null || c.CompanyId != null
        || Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id);

    public static int CountTroopsOfType(Army army, string type) => type switch
    {
        "HeavyCavalry" => army.HeavyCavalry,
        "LightCavalry" => army.LightCavalry,
        "HeavyInfantry" => army.HeavyInfantry,
        "LightInfantry" => army.LightInfantry,
        "Archers" => army.Archers,
        "MenAtArms" => army.MenAtArms,
        _ => 0
    };

    public static int StockLevel(Nation nation, string product) => product switch
    {
        "timber" => nation.Timber,
        "leather" => nation.Leather,
        "bronze" => nation.Bronze,
        "steel" => nation.Steel,
        "mithril" => nation.Mithril,
        "mounts" => nation.Mounts,
        "food" => nation.Food,
        _ => 0
    };

    // ---- option lists for forms ----
    public List<OrderFieldOptionDto> CharacterOptions(IEnumerable<Character> chars) => chars
        .Select(c => new OrderFieldOptionDto(c.Id, $"{c.Name} ({OrderTexts.CharTypeName(Lang, c.Type)} @ {c.LocationHex})"))
        .ToList();

    public List<OrderFieldOptionDto> ArmyOptions(string? atHex = null) => Nation.Armies
        .Where(a => atHex == null || a.LocationHex == atHex)
        .Select(a => new OrderFieldOptionDto(a.Id, $"{a.Name} @ {a.LocationHex}"))
        .ToList();

    public List<OrderFieldOptionDto> KnownSpellOptions(Services.SpellType type) => Character.Spells
        .Where(s => s.IsKnown && !s.IsLost && Services.SpellCatalog.Get(s.SpellId)?.Type == type)
        .Select(s => new OrderFieldOptionDto(s.SpellId.ToString(),
            $"#{s.SpellId} {OrderTexts.SpellDisplayName(Lang, s.SpellId, Services.SpellCatalog.Get(s.SpellId)?.Name ?? "?")}"))
        .ToList();

    public List<OrderFieldOptionDto> AllNations() => Game.Nations
        .Select(n => new OrderFieldOptionDto(n.Id, $"{OrderTexts.NationDisplayName(Lang, n.Name)} ({OrderTexts.AllegianceName(Lang, n.Allegiance)})"))
        .ToList();

    public List<OrderFieldOptionDto> HeldArtifacts() => Character.Artifacts
        .Where(a => a.HeldByCharacterId == Character.Id)
        .Select(a => new OrderFieldOptionDto(a.Id, OrderTexts.ArtifactDisplayName(Lang, a)))
        .ToList();

    public List<OrderFieldOptionDto> KnownSpellOptionsForForget() => Character.Spells
        .Where(s => s.IsKnown && !s.IsLost)
        .Select(s => new OrderFieldOptionDto(s.SpellId.ToString(),
            $"#{s.SpellId} {OrderTexts.SpellDisplayName(Lang, s.SpellId, Services.SpellCatalog.Get(s.SpellId)?.Name ?? "?")}"))
        .ToList();

    public List<OrderFieldOptionDto> ResearchableSpellOptions() =>
        Services.SpellCatalog.All
            .Where(s => !Character.Spells.Any(x => x.SpellId == s.Id && x.IsKnown && !x.IsLost)
                && (!s.IsLost || Domain.Constants.NationAbilities.CanLearnLostSpell(Nation.Name, s.Id)))
            .Select(s => new OrderFieldOptionDto(s.Id.ToString(),
                $"#{s.Id} {OrderTexts.SpellDisplayName(Lang, s.Id, s.Name)}{(s.IsLost ? OrderTexts.Get(Lang, "opt.lost") : "")}"))
            .ToList();
}
