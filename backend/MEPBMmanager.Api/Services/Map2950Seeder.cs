using System.Text.Json;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;

namespace MEPBMmanager.Api.Services;

/// <summary>
/// Seeds the real Third Age 2950 module (middle-earth.wiki / MEPBM) from
/// map2950_initial.json: 1716 hex tiles, 25 real nations, 135 population
/// centres and 64 starting army/navy stacks, matching the official module.
/// </summary>
public static class Map2950Seeder
{
    public const string JsonFileName = "Data/map2950_initial.json";

    /// <summary>JSON terrain class → backend HexTile terrain vocabulary.</summary>
    private static readonly Dictionary<string, string> TerrainMap = new()
    {
        ["plain"] = "plains",
        ["mountain"] = "mountains",
        ["shore"] = "shore",
        ["forest"] = "forest",
        ["swamp"] = "swamp",
        ["rough"] = "rough",
        ["desert"] = "desert",
        ["coastal"] = "coastal",
        ["ocean"] = "water"
    };

    /// <summary>Real 2950 nations in module order (free, dark, neutral).</summary>
    private static readonly (string Slug, string Name, string Allegiance, string Color)[] NationOrder =
    {
        ("woodmen", "Woodmen", "free_peoples", "#556B2F"),
        ("northmen", "Northmen", "free_peoples", "#4169E1"),
        ("riders-of-rohan", "Riders of Rohan", "free_peoples", "#FFD700"),
        ("dunadan-rangers", "Dúnadan Rangers", "free_peoples", "#2E8B57"),
        ("silvan-elves", "Silvan Elves", "free_peoples", "#DA70D6"),
        ("northern-gondor", "Northern Gondor", "free_peoples", "#1E90FF"),
        ("southern-gondor", "Southern Gondor", "free_peoples", "#6495ED"),
        ("dwarves", "Dwarves", "free_peoples", "#A0522D"),
        ("sinda-elves", "Sinda Elves", "free_peoples", "#7B68EE"),
        ("noldo-elves", "Noldo Elves", "free_peoples", "#9370DB"),
        ("witch-king", "Witch-king", "dark_servants", "#8B0000"),
        ("dragon-lord", "Dragon Lord", "dark_servants", "#FF4500"),
        ("dog-lord", "Dog Lord", "dark_servants", "#B8860B"),
        ("cloud-lord", "Cloud Lord", "dark_servants", "#708090"),
        ("blind-sorcerer", "Blind Sorcerer", "dark_servants", "#4B0082"),
        ("ice-king", "Ice King", "dark_servants", "#00CED1"),
        ("quiet-avenger", "Quiet Avenger", "dark_servants", "#483D8B"),
        ("fire-king", "Fire King", "dark_servants", "#FF6347"),
        ("long-rider", "Long Rider", "dark_servants", "#20B2AA"),
        ("dark-lieutenants", "Dark Lieutenants", "dark_servants", "#2F2F2F"),
        ("dunlendings", "Dunlendings", "neutral", "#9ACD32"),
        ("khand-easterlings", "Khand Easterlings", "neutral", "#BDB76B"),
        ("rhun-easterlings", "Rhûn Easterlings", "neutral", "#CD853F"),
        ("white-wizard", "White Wizard", "neutral", "#F5F5F5"),
        ("corsairs", "Corsairs", "neutral", "#5F9EA0")
    };

    // ── Plantillas por baremo de jugadores (conjuntos anidados: cada baremo ──
    // añade naciones al anterior). Recorte por laterales con Mordor+Gondor
    // siempre dentro. Solo el mapa de 25 es completo.
    public sealed record TemplateDef(
        string[] Free, string[] Dark, string[] Neutral,
        int Q1, int Q2, int R1, int R2);

    public static readonly Dictionary<int, TemplateDef> Templates = new()
    {
        [10] = new(
            ["riders-of-rohan", "northern-gondor", "southern-gondor", "sinda-elves"],
            ["dark-lieutenants", "dog-lord", "fire-king", "cloud-lord"],
            ["white-wizard", "dunlendings"],
            13, 37, 12, 31),
        [15] = new(
            ["riders-of-rohan", "northern-gondor", "southern-gondor", "sinda-elves", "woodmen", "northmen"],
            ["dark-lieutenants", "dog-lord", "fire-king", "cloud-lord", "ice-king", "dragon-lord"],
            ["white-wizard", "dunlendings", "rhun-easterlings"],
            13, 44, 4, 31),
        [20] = new(
            ["riders-of-rohan", "northern-gondor", "southern-gondor", "sinda-elves", "woodmen", "northmen", "dunadan-rangers", "silvan-elves"],
            ["dark-lieutenants", "dog-lord", "fire-king", "cloud-lord", "ice-king", "dragon-lord", "blind-sorcerer", "long-rider"],
            ["white-wizard", "dunlendings", "rhun-easterlings", "khand-easterlings"],
            7, 44, 4, 38),
        [25] = new(
            ["riders-of-rohan", "northern-gondor", "southern-gondor", "sinda-elves", "woodmen", "northmen", "dunadan-rangers", "silvan-elves", "dwarves", "noldo-elves"],
            ["dark-lieutenants", "dog-lord", "fire-king", "cloud-lord", "ice-king", "dragon-lord", "blind-sorcerer", "long-rider", "witch-king", "quiet-avenger"],
            ["white-wizard", "dunlendings", "rhun-easterlings", "khand-easterlings", "corsairs"],
            1, 44, 1, 39),
    };

    // Baremo por confirmados: 21-25→25, 16-20→20, 11-15→15, 6-10→10.
    public static int TemplateSizeFor(int confirmedCount) => confirmedCount switch
    {
        >= 21 and <= 25 => 25,
        >= 16 and <= 20 => 20,
        >= 11 and <= 15 => 15,
        >= 6 and <= 10 => 10,
        _ => throw new InvalidOperationException(
            "2950 needs 6-10, 11-15, 16-20 or 21-25 confirmed players")
    };

    // Naciones sobrantes (0-4) como PNJ, priorizando neutrales:
    // 1→1N, 2→2N, 3→1F+1D+1N, 4→1F+1D+2N (siempre del final de cada lista).
    // Devuelve (jugadas, pnj). Los PNJ pueden incorporarse luego (nación libre).
    public static (string[] Played, string[] Npcs) SplitNpcs(TemplateDef t, int confirmedCount)
    {
        int templateSize = t.Free.Length + t.Dark.Length + t.Neutral.Length;
        int surplus = templateSize - confirmedCount;
        var npcs = new List<string>();
        if (surplus == 1) npcs.Add(t.Neutral[^1]);
        else if (surplus == 2) npcs.AddRange(t.Neutral.TakeLast(2));
        else if (surplus >= 3)
        {
            npcs.Add(t.Free[^1]);
            npcs.Add(t.Dark[^1]);
            npcs.Add(t.Neutral[^1]);
            if (surplus >= 4) npcs.Add(t.Neutral[^2]);
        }
        var npcSet = new HashSet<string>(npcs);
        var played = t.Free.Concat(t.Dark).Concat(t.Neutral).Where(s => !npcSet.Contains(s)).ToArray();
        return (played, npcs.ToArray());
    }

    public sealed record CentreSeed(
        string NationSlug, int Q, int R, string Name, string Size, string Fortification,
        bool HasHarbour, bool HasPort, bool IsCapital, bool IsHidden);

    public sealed record ArmySeed(
        string NationSlug, int Q, int R,
        int HeavyCavalry, int LightCavalry, int HeavyInfantry, int LightInfantry,
        int Archers, int MenAtArms, int Warships, int Transports, int Morale,
        int Training = 60, int Weapons = 30, int Armour = 0, int Food = 1000)
    {
        public int TotalLandTroops => HeavyCavalry + LightCavalry + HeavyInfantry + LightInfantry + Archers + MenAtArms;
    }

    public sealed record NationMeta(string Slug, string DisplayName, string Allegiance, string Color, string? CapitalHex);

    public sealed record CharacterSeed(
        string NationSlug, string Name, string Type,
        int Command, int Agent, int Emissary, int Mage,
        int Stealth, int Challenge, bool IsChampion, int Q, int R,
        int[] SpellIds, string? CommandsAt, int[] ArtifactIds,
        Dictionary<int, int> SpellRanks);

    public sealed record NationStatsSeed(
        string Slug, int Gold, int Food, int Timber, int Leather,
        int Bronze, int Steel, int Mithril, int Mounts, int TaxRate,
        int VictoryPoints = 0, int WarshipStrength = 3);

    public sealed record ArtifactSeed(
        int Id, string Name, string Type, string Alignment, string Power,
        string NationSlug, string Holder);

    public sealed class Map2950Data
    {
        public List<(int Q, int R, string Terrain, bool HasBridge, bool HasFord, bool HasMajorRiver, bool HasMinorRiver, bool HasRoad)> Hexes { get; } = new();
        public List<CentreSeed> Centres { get; } = new();
        public List<ArmySeed> Armies { get; } = new();
        public List<CharacterSeed> Characters { get; } = new();
        public List<NationMeta> Nations { get; } = new();
        public Dictionary<string, NationStatsSeed> NationStats { get; } = new();
        public Dictionary<int, ArtifactSeed> Artifacts { get; } = new();
    }

    private static int ParseTroops(JsonElement e)
    {
        var s = e.GetString();
        if (string.IsNullOrWhiteSpace(s) || s == "-")
            return 0;
        return int.TryParse(s, out var n) ? n : 0;
    }

    public static Map2950Data Load(string baseDir)
    {
        var path = Path.Combine(baseDir, JsonFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"2950 map data not found: {path}");

        var data = new Map2950Data();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (!root.TryGetProperty("hexes", out var hexes))
            return data;

        foreach (var h in hexes.EnumerateObject())
        {
            var e = h.Value;
            var q = e.GetProperty("q").GetInt32();
            var r = e.GetProperty("r").GetInt32();
            var terrain = e.GetProperty("terrain").GetString() ?? "plains";
            var hasBridge = e.TryGetProperty("bridge", out var br) && br.ValueKind == JsonValueKind.True;
            var hasFord = e.TryGetProperty("ford", out var fo) && fo.ValueKind == JsonValueKind.True;
            var hasMajor = e.TryGetProperty("majorRiver", out var mj) && mj.ValueKind == JsonValueKind.True;
            var hasMinor = e.TryGetProperty("minorRiver", out var mn) && mn.ValueKind == JsonValueKind.True;
            var hasRoad = e.TryGetProperty("road", out var ro) && ro.ValueKind == JsonValueKind.True;
            data.Hexes.Add((q, r, TerrainMap.TryGetValue(terrain, out var mapped) ? mapped : terrain, hasBridge, hasFord, hasMajor, hasMinor, hasRoad));

            if (e.TryGetProperty("populationCentre", out var pc) && pc.ValueKind == JsonValueKind.Object)
            {
                var slug = pc.TryGetProperty("nation", out var n) ? n.GetString() ?? "" : "";
                var size = pc.TryGetProperty("size", out var sz) ? sz.GetString() ?? "Village" : "Village";
                var fort = pc.TryGetProperty("fort", out var ft) ? ft.GetString() ?? "-" : "-";
                var harbour = pc.TryGetProperty("harbour", out var hb) ? hb.GetString() ?? "-" : "-";
                var special = pc.TryGetProperty("special", out var sp) ? sp.GetString() ?? "-" : "-";

                data.Centres.Add(new CentreSeed(
                    slug, q, r,
                    pc.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                    size, fort,
                    harbour == "Harbour",
                    harbour == "Port",
                    special.Contains("Capital", StringComparison.OrdinalIgnoreCase),
                    special.Contains("Hidden", StringComparison.OrdinalIgnoreCase)));
            }

            if (e.TryGetProperty("armies", out var armies) && armies.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in armies.EnumerateArray())
                {
                    var slug = a.TryGetProperty("nation", out var n2) ? n2.GetString() ?? "" : "";
                    int IntProp(string p, int def)
                    {
                        if (!a.TryGetProperty(p, out var el)) return def;
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
                        return ParseTroops(el);
                    }
                    data.Armies.Add(new ArmySeed(
                        slug, q, r,
                        ParseTroops(a.GetProperty("HC")),
                        ParseTroops(a.GetProperty("LC")),
                        ParseTroops(a.GetProperty("HI")),
                        ParseTroops(a.GetProperty("LI")),
                        ParseTroops(a.GetProperty("AR")),
                        ParseTroops(a.GetProperty("MA")),
                        ParseTroops(a.GetProperty("warships")),
                        ParseTroops(a.GetProperty("transports")),
                        a.TryGetProperty("morale", out var ml) ? ParseTroops(ml) : 60,
                        IntProp("training", 60),
                        IntProp("weapons", 40),
                        IntProp("armour", 0),
                        IntProp("food", 1000)));
                }
            }
        }

        foreach (var (slug, name, allegiance, color) in NationOrder)
        {
            var capital = data.Centres.FirstOrDefault(c => c.NationSlug == slug && c.IsCapital);
            data.Nations.Add(new NationMeta(slug, name, allegiance, color,
                capital != null ? $"{capital.Q},{capital.R}" : null));
        }

        // ── Personajes iniciales del módulo (wiki.mepbm.com/2950 + Game 299 Turn 0 PDFs) ──
        // JSON: {nation, name, type, command, agent, emissary, mage, stealth,
        //        challenge, hex:"Q,R", spells:[...], spellRanks:{...}, artifacts:[...], commands:"Q,R"}.
        // Campeón = mayor mando (empate: primero).
        if (root.TryGetProperty("characters", out var chars) && chars.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in chars.EnumerateArray())
            {
                string S(string p) => c.TryGetProperty(p, out var e) ? e.GetString() ?? "" : "";
                int N(string p) => c.TryGetProperty(p, out var e) && e.TryGetInt32(out var n) ? n : 0;
                var qr = S("hex").Split(',');
                int.TryParse(qr.ElementAtOrDefault(0), out var cq);
                int.TryParse(qr.ElementAtOrDefault(1), out var cr);
                int[] spells = [];
                if (c.TryGetProperty("spells", out var sp) && sp.ValueKind == JsonValueKind.Array)
                    spells = sp.EnumerateArray().Where(e => e.TryGetInt32(out _)).Select(e => e.GetInt32()).ToArray();
                int[] artifacts = [];
                if (c.TryGetProperty("artifacts", out var ar) && ar.ValueKind == JsonValueKind.Array)
                    artifacts = ar.EnumerateArray().Where(e => e.TryGetInt32(out _)).Select(e => e.GetInt32()).ToArray();
                var spellRanks = new Dictionary<int, int>();
                if (c.TryGetProperty("spellRanks", out var sr) && sr.ValueKind == JsonValueKind.Object)
                    foreach (var prop in sr.EnumerateObject())
                        if (int.TryParse(prop.Name, out var sid) && prop.Value.TryGetInt32(out var rank))
                            spellRanks[sid] = rank;
                data.Characters.Add(new CharacterSeed(
                    S("nation"), S("name"), S("type"),
                    N("command"), N("agent"), N("emissary"), N("mage"),
                    N("stealth"), N("challenge"), false, cq, cr, spells, S("commands") is { } cmd && cmd != "" ? cmd : null, artifacts, spellRanks));
            }
            foreach (var g in data.Characters.GroupBy(c => c.NationSlug))
            {
                var boss = g.OrderByDescending(c => c.Command).First();
                data.Characters[data.Characters.IndexOf(boss)] = boss with { IsChampion = true };
            }
        }

        // ── Recursos iniciales por nación (Game 299 Turn 0: gold reserve + stores + tax) ──
        if (root.TryGetProperty("nationStats", out var ns) && ns.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in ns.EnumerateObject())
            {
                var v = prop.Value;
                // JSON usa minúsculas (gold, food...); aceptamos también Gold/Food por compatibilidad
                int Alt(string lower, string upper, int def = 0)
                {
                    if (v.TryGetProperty(lower, out var e1) && e1.TryGetInt32(out var n1)) return n1;
                    if (v.TryGetProperty(upper, out var e2) && e2.TryGetInt32(out var n2)) return n2;
                    return def;
                }
                data.NationStats[prop.Name] = new NationStatsSeed(
                    prop.Name,
                    Alt("gold", "Gold", 10000), Alt("food", "Food", 5000),
                    Alt("timber", "Timber", 2000), Alt("leather", "Leather", 1000),
                    Alt("bronze", "Bronze", 500), Alt("steel", "Steel", 200),
                    Alt("mithril", "Mithril", 50), Alt("mounts", "Mounts", 300),
                    Alt("taxRate", "TaxRate", 40),
                    Alt("victoryPoints", "VictoryPoints", 0),
                    Alt("warshipStrength", "WarshipStrength", 3));
            }
        }

        // ── Catálogo de artefactos iniciales (Game 299 Turn 0) ──
        if (root.TryGetProperty("artifacts", out var arts) && arts.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in arts.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var aid)) continue;
                var v = prop.Value;
                string GS(string p) => v.TryGetProperty(p, out var e) ? e.GetString() ?? "" : "";
                data.Artifacts[aid] = new ArtifactSeed(
                    aid, GS("name"), GS("type"), GS("alignment"), GS("power"),
                    GS("nation"), GS("holder"));
            }
        }

        return data;
    }

    /// <summary>Inserts real hex tiles for a 2950 game, cropped to the template box.</summary>
    public static void CreateMap(string gameId, string gameTypeId, Map2950Data data, MepbmDbContext db,
        (int Q1, int Q2, int R1, int R2)? crop = null)
    {
        foreach (var (q, r, terrain, hasBridge, hasFord, hasMajor, hasMinor, hasRoad) in data.Hexes)
        {
            if (crop.HasValue && (q < crop.Value.Q1 || q > crop.Value.Q2 || r < crop.Value.R1 || r > crop.Value.R2))
                continue;
            db.HexTiles.Add(new HexTile
            {
                Id = Guid.NewGuid().ToString(),
                GameId = gameId,
                GameTypeId = gameTypeId,
                Q = q,
                R = r,
                Terrain = terrain,
                HasBridge = hasBridge,
                HasFord = hasFord,
                HasMajorRiver = hasMajor,
                HasMinorRiver = hasMinor,
                HasRoad = hasRoad
            });
        }
    }
}