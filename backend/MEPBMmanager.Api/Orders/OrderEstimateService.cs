using System.Text.Json;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Orders;

/// <summary>
/// Order rules: eligibility, per-order forms (<see cref="RequiresFor"/>),
/// validation, cross-order conflicts and live cost estimates.
/// No HTTP here: the controller only maps requests/responses.
/// All user-facing strings live in <see cref="OrderTexts"/>.
/// </summary>
public sealed class OrderEstimateService
{
    private readonly MepbmDbContext _db;

    public OrderEstimateService(MepbmDbContext db)
    {
        _db = db;
    }

    public (bool Ok, string Reason) CheckEligible(Character ch, OrderDefinition def,
        Nation? nation, bool commandsNavy, string? lang = null)
    {
        if (ch.IsDead || ch.IsKidnapped) return (false, OrderTexts.Get(lang, "err.character-cannot-act"));
        if (def.Code == 100) return (false, OrderTexts.Get(lang, "reason.automatic-order"));
        // Prerrequisitos de estado: van antes que las restricciones de tipo,
        // aplican a todas las órdenes.
        var need0 = def.Code switch
        {
            205 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id && TurnProcessor.CombatArtifactTypes.Contains(a.Type ?? "")) ? "" : OrderTexts.Get(lang, "reason.no-combat-artifact-held"),
            360 or 792 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id) ? "" : OrderTexts.Get(lang, "reason.no-artifact-held"),
            750 or 760 => ch.CompanyId != null ? "" : OrderTexts.Get(lang, "reason.not-in-a-company"),
            790 => ch.ArmyId != null ? "" : OrderTexts.Get(lang, "reason.not-in-an-army"),
            120 => HasSpellType(ch, SpellType.Heal) ? "" : OrderTexts.Get(lang, "reason.no-healing-spell-known"),
            225 => HasSpellType(ch, SpellType.Combat) ? "" : OrderTexts.Get(lang, "reason.no-combat-spell-known"),
            330 => HasSpellType(ch, SpellType.Conjuring) ? "" : OrderTexts.Get(lang, "reason.no-conjuring-spell-known"),
            825 => HasSpellType(ch, SpellType.Movement) ? "" : OrderTexts.Get(lang, "reason.no-movement-spell-known"),
            940 => HasSpellType(ch, SpellType.Lore) ? "" : OrderTexts.Get(lang, "reason.no-lore-spell-known"),
            _ => ""
        };
        if (need0 != "") return (false, need0);
        var armyNeeded = new HashSet<int> { 230, 235, 240, 250, 255, 260, 340, 345, 347, 349, 351, 353, 355, 370, 375, 400, 404, 408, 412, 416, 420, 425, 430, 435, 440, 444, 448, 765, 775, 780, 840, 850, 860 };
        if (armyNeeded.Contains(def.Code) && ch.ArmyId == null)
            return (false, OrderTexts.Get(lang, "reason.not-in-an-army"));
        var navyNeeded = new HashSet<int> { 270, 275, 280, 830, 794, 798 };
        if (navyNeeded.Contains(def.Code) && (nation == null || nation.Navies.Count == 0))
            return (false, OrderTexts.Get(lang, "reason.no-navy-available"));
        if (def.Code == 830 && !commandsNavy)
            return (false, OrderTexts.Get(lang, "reason.must-command-a-navy"));
        var capHex = nation?.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
        var atCapital = capHex != null && capHex == ch.LocationHex;
        if ((def.Code is 175 or 180 or 185 or 280 or 300 or 325 or 660 or 725 or 728 or 731 or 734 or 737) && !atCapital)
            return (false, OrderTexts.Get(lang, "reason.must-be-at-your-own-capital"));
        if (def.Code == 950 && ch.LocationHex != capHex)
            return (false, OrderTexts.Get(lang, "reason.must-be-at-your-current-capital"));
        if ((def.Code is 520 or 530 or 535 or 550 or 705 or 710)
            && (nation == null || !nation.PopulationCentres.Any(p => p.LocationHex == ch.LocationHex && p.NationId == nation.Id)))
            return (false, OrderTexts.Get(lang, "reason.must-be-at-one-of-your-population-centres"));
        var r = def.Restrictions;
        if (r.Length == 0 || r.Contains("without")) return (true, "");
        var type = (ch.Type ?? "").ToLower();
        foreach (var t in r)
        {
            switch (t)
            {
                case "c": if (type == "commander" || ch.CommandSkill > 0) return (true, ""); break;
                case "a": if (type == "agent" || ch.AgentSkill > 0) return (true, ""); break;
                case "e": if (type == "emissary" || ch.EmissarySkill > 0) return (true, ""); break;
                case "m": if (type == "mage" || ch.MageSkill > 0) return (true, ""); break;
                case "com":
                    if (ch.ArmyId != null || commandsNavy) return (true, "");
                    break;
                case "company": if (ch.CompanyId != null) return (true, ""); break;
                case "cap": if (capHex != null && capHex == ch.LocationHex) return (true, ""); break;
                case "fa": return (false, OrderTexts.Get(lang, "reason.fourth-age-only"));
            }
        }
        return (false, OrderTexts.Get(lang, "reason.requires") + string.Join("/", r));
    }

    private static bool HasSpellType(Character ch, SpellType type) =>
        ch.Spells.Any(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == type);

    private static readonly HashSet<int> MoveCodes = new() { 810, 820, 830, 850, 860, 870, 825 };

    private static Dictionary<string, System.Text.Json.JsonElement> ParseEstimateParams(System.Text.Json.JsonElement? el)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (el.HasValue && el.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var p in el.Value.EnumerateObject()) d[p.Name] = p.Value;
        return d;
    }

    private Army? ResolveEstimateArmy(string? armyId, Character ch, Nation nation)
    {
        if (!string.IsNullOrEmpty(armyId))
            return nation.Armies.FirstOrDefault(a => a.Id == armyId);
        if (!string.IsNullOrEmpty(ch.ArmyId))
            return nation.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
        return null;
    }

    private Navy? ResolveEstimateNavy(string? navyId, Character ch, Nation nation)
    {
        if (!string.IsNullOrEmpty(navyId))
            return nation.Navies.FirstOrDefault(v => v.Id == navyId);
        return nation.Navies.FirstOrDefault(v => v.CommanderId == ch.Id);
    }

    private static int ParamInt(Dictionary<string, System.Text.Json.JsonElement> pars, string key, int def = 0)
    {
        if (pars.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        return def;
    }

    private static string? ParamStr(Dictionary<string, System.Text.Json.JsonElement> pars, string key)
    {
        if (pars.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static OrderFieldSpecDto Num(string k, string l, bool req = true, int? min = null, int? max = null) =>
        new(k, l, "number", req, min, max);
    private static OrderFieldSpecDto Txt(string k, string l, bool req = true) =>
        new(k, l, "text", req);
    private static OrderFieldSpecDto Hx(string k, string l, bool req) =>
        new(k, l, "hex", req);
    private static OrderFieldSpecDto Sel(string k, string l, List<OrderFieldOptionDto> opts, bool req = true) =>
        new(k, l, "select", req, Options: opts);
    private static OrderFieldSpecDto Multi(string k, string l, List<OrderFieldOptionDto> opts) =>
        new(k, l, "multiselect", false, Options: opts);
    private static OrderFieldSpecDto Flag(string k, string l) =>
        new(k, l, "flag", false);

    private static readonly List<OrderFieldOptionDto> TacticOptions = new()
    {
        new("ch", "ch"), new("su", "su"), new("fl", "fl"),
        new("hr", "hr"), new("am", "am"), new("st", "st")
    };
    private static List<OrderFieldOptionDto> AllegianceOptions(string? lang) => new()
    {
        new("free_peoples", OrderTexts.Get(lang, "opt.free-peoples")),
        new("dark_servants", OrderTexts.Get(lang, "opt.dark-servants")),
        new("neutral", OrderTexts.Get(lang, "opt.neutral"))
    };
    private static List<OrderFieldOptionDto> MaterialOptions(string? lang) => new()
    {
        new("leather", OrderTexts.Get(lang, "opt.leather-10")), new("bronze", OrderTexts.Get(lang, "opt.bronze-30")),
        new("steel", OrderTexts.Get(lang, "opt.steel-60")), new("mithril", OrderTexts.Get(lang, "opt.mithril-100"))
    };
    private static List<OrderFieldOptionDto> TroopTypeOptions(string? lang) => new()
    {
        new("HeavyCavalry", OrderTexts.Get(lang, "opt.heavy-cavalry")),
        new("LightCavalry", OrderTexts.Get(lang, "opt.light-cavalry")),
        new("HeavyInfantry", OrderTexts.Get(lang, "opt.heavy-infantry")),
        new("LightInfantry", OrderTexts.Get(lang, "opt.light-infantry")),
        new("Archers", OrderTexts.Get(lang, "opt.archers")),
        new("MenAtArms", OrderTexts.Get(lang, "opt.men-at-arms"))
    };
    private static List<OrderFieldOptionDto> ProductOptions(string? lang) =>
        TurnProcessor.MarketProductList().Select(p => new OrderFieldOptionDto(p, ProductName(lang, p))).ToList();
    private static string ProductName(string? lang, string p) => p.ToLower() switch
    {
        "timber" => OrderTexts.Get(lang, "cost.timber"),
        "leather" => OrderTexts.Get(lang, "cost.leather"),
        "bronze" => OrderTexts.Get(lang, "cost.bronze"),
        "steel" => OrderTexts.Get(lang, "cost.steel"),
        "mithril" => OrderTexts.Get(lang, "cost.mithril"),
        "mounts" => OrderTexts.Get(lang, "cost.mounts"),
        "food" => OrderTexts.Get(lang, "cost.food"),
        _ => p
    };
    private static List<OrderFieldOptionDto> LevelOptions(string? lang) => new()
    {
        new("1", OrderTexts.Get(lang, "opt.1-tower")), new("2", OrderTexts.Get(lang, "opt.2-fort")),
        new("3", OrderTexts.Get(lang, "opt.3-castle")), new("4", OrderTexts.Get(lang, "opt.4-keep")),
        new("5", OrderTexts.Get(lang, "opt.5-citadel"))
    };
    private static string AllegianceName(string? lang, string? al) => (al ?? "").ToLower() switch
    {
        "free_peoples" or "free" => OrderTexts.Get(lang, "opt.free-peoples"),
        "dark_servants" or "dark" => OrderTexts.Get(lang, "opt.dark-servants"),
        "neutral" => OrderTexts.Get(lang, "opt.neutral"),
        _ => al ?? "?"
    };
    private static readonly Dictionary<string, string> NationEs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Woodmen"] = "Hombres del Bosque",
        ["Northmen"] = "Hombres del Norte",
        ["Riders of Rohan"] = "Jinetes de Rohan",
        ["Dúnadan Rangers"] = "Montaraces Dúnedain",
        ["Dunadan Rangers"] = "Montaraces Dúnedain",
        ["Silvan Elves"] = "Elfos Silvanos",
        ["Northern Gondor"] = "Gondor del Norte",
        ["Southern Gondor"] = "Gondor del Sur",
        ["Dwarves"] = "Enanos",
        ["Sinda Elves"] = "Elfos Sindar",
        ["Noldo Elves"] = "Elfos Noldor",
        ["Witch-king"] = "Rey Brujo",
        ["Witch King"] = "Rey Brujo",
        ["Dragon Lord"] = "Señor de los Dragones",
        ["Dog Lord"] = "Señor de los Perros",
        ["Cloud Lord"] = "Señor de las Nubes",
        ["Blind Sorcerer"] = "Hechicero Ciego",
        ["Ice King"] = "Rey de Hielo",
        ["Quiet Avenger"] = "Vengador Silencioso",
        ["Fire King"] = "Rey del Fuego",
        ["Long Rider"] = "Jinete Largo",
        ["Dark Lieutenants"] = "Lugartenientes Oscuros",
        ["Corsairs"] = "Corsarios",
        ["Rhûn Easterlings"] = "Orientales de Rhûn",
        ["Rhun Easterlings"] = "Orientales de Rhûn",
        ["Dunlendings"] = "Dunlendinos",
        ["White Wizard"] = "Mago Blanco",
        ["Khand Easterlings"] = "Orientales de Khand",
    };
    private static string NationDisplayName(string? lang, string name) =>
        lang == "es" && NationEs.TryGetValue(name ?? "", out var es) ? es : name;
    private static string SpellDisplayName(string? lang, int spellId, string fallback) =>
        lang == "es" && SpellDefinitionsEs.NamesEs.TryGetValue(spellId, out var n) ? n : fallback;
    private static string ArtifactDisplayName(string? lang, Artifact a) =>
        lang == "es" ? (ArtifactCatalog2950Es.NameEsByName(a.Name) ?? a.Name) : a.Name;
    private static string CharTypeName(string? lang, string? t) => (t ?? "").ToLower() switch
    {
        "commander" => OrderTexts.Get(lang, "opt.commander"),
        "agent" => OrderTexts.Get(lang, "opt.agent"),
        "emissary" => OrderTexts.Get(lang, "opt.emissary"),
        "mage" => OrderTexts.Get(lang, "opt.mage"),
        _ => t ?? "?"
    };

    // Hechizos de lore por tipo de diana (wiki: Required Information).
    private static readonly HashSet<int> LoreCharSpells = new() { 408, 420, 422, 424, 430, 436 };
    private static readonly HashSet<int> LoreCommanderSpells = new() { 406, 417, 426 };
    private static readonly HashSet<int> LoreNationSpells = new() { 404, 419, 432 };
    private static readonly HashSet<int> LoreAllegianceSpells = new() { 402, 410 };
    private static readonly HashSet<int> LoreArtifactSpells = new() { 412, 418, 428 };

    public List<OrderFieldSpecDto> RequiresFor(int code, EstimateCtx ctx)
    {
        var ch = ctx.Ch;
        var lng = ctx.Lang;
        List<OrderFieldOptionDto> SpellOpts(SpellType t) => ch.Spells
            .Where(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == t)
            .Select(s => new OrderFieldOptionDto(s.SpellId.ToString(),
                $"#{s.SpellId} {SpellDisplayName(lng, s.SpellId, SpellCatalog.Get(s.SpellId)?.Name ?? "?")}"))
            .ToList();
        List<OrderFieldOptionDto> ArmyOpts(string? atHex = null) => ctx.Nation.Armies
            .Where(a => atHex == null || a.LocationHex == atHex)
            .Select(a => new OrderFieldOptionDto(a.Id, $"{a.Name} @ {a.LocationHex}"))
            .ToList();
        List<OrderFieldOptionDto> CharOpts(IEnumerable<Character> chars) => chars
            .Select(c => new OrderFieldOptionDto(c.Id, $"{c.Name} ({CharTypeName(lng, c.Type)} @ {c.LocationHex})"))
            .ToList();
        var ownChars = ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId == ctx.Nation.Id && !c.IsDead).ToList();
        var foeCharsAtLoc = ctx.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != ctx.Nation.Id && !c.IsDead && c.LocationHex == ctx.EffLoc).ToList();
        var allNations = ctx.Game.Nations
            .Select(n => new OrderFieldOptionDto(n.Id, $"{NationDisplayName(lng, n.Name)} ({AllegianceName(lng, n.Allegiance)})")).ToList();
        var heldArts = ch.Artifacts.Where(a => a.HeldByCharacterId == ch.Id)
            .Select(a => new OrderFieldOptionDto(a.Id, ArtifactDisplayName(lng, a))).ToList();
        var catalogSpells = SpellCatalog.All
            .Select(s => new OrderFieldOptionDto(s.Id.ToString(), $"#{s.Id} {SpellDisplayName(lng, s.Id, s.Name)}{(s.IsLost ? OrderTexts.Get(lng, "opt.lost") : "")}")).ToList();
        // 940: el formulario se criba según el hechizo elegido.
        List<OrderFieldSpecDto> LoreRequires()
        {
            var fields = new List<OrderFieldSpecDto>
                { Sel("spellId", OrderTexts.Get(lng, "label.spell"), SpellOpts(SpellType.Lore)) };
            var sid = ParamInt(ctx.Pars, "spellId", -1);
            if (LoreCharSpells.Contains(sid))
                fields.Add(Sel("targetId", OrderTexts.Get(lng, "label.target-character"),
                    CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))));
            else if (LoreCommanderSpells.Contains(sid))
                fields.Add(Sel("commanderId", OrderTexts.Get(lng, "label.force-commander"),
                    CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead
                        && (c.ArmyId != null || ctx.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id))))));
            else if (LoreNationSpells.Contains(sid))
                fields.Add(Sel("nationId", OrderTexts.Get(lng, "label.target-nation"), allNations));
            else if (LoreAllegianceSpells.Contains(sid))
                fields.Add(Sel("allegiance", OrderTexts.Get(lng, "label.allegiance"), AllegianceOptions(lng)));
            else if (LoreArtifactSpells.Contains(sid))
                fields.Add(Sel("artifactId", OrderTexts.Get(lng, "label.artifact"), _db.Artifacts.ToList()
                    .Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId != null
                        ? $"{ArtifactDisplayName(lng, a)} {OrderTexts.Get(lng, "opt.held")}" : $"{ArtifactDisplayName(lng, a)} @ {a.LocationHex ?? "?"}")).ToList()));
            else
                fields.Add(Hx("hex", OrderTexts.Get(lng, "label.hex-empty-here"), req: false));
            return fields;
        }

        return code switch
        {
            175 => new() { Sel("allegiance", OrderTexts.Get(lng, "label.allegiance"), AllegianceOptions(lng)) },
            180 or 185 => new() { Sel("nationId", OrderTexts.Get(lng, "label.nation"), allNations) },
            690 => new() { Sel("nationId", OrderTexts.Get(lng, "label.victim-nation"), allNations), Num("amount", OrderTexts.Get(lng, "label.gold-amount"), min: 1) },
            210 or 615 or 620 => new() { Sel("targetId", OrderTexts.Get(lng, "label.target-character"), CharOpts(foeCharsAtLoc)) },
            225 or 330 => new() { Sel("spellId", OrderTexts.Get(lng, "label.spell"),
                code == 225 ? SpellOpts(SpellType.Combat) : SpellOpts(SpellType.Conjuring)) },
            120 => new() { Sel("spellId", OrderTexts.Get(lng, "label.spell"), SpellOpts(SpellType.Heal)),
                Sel("targetId", OrderTexts.Get(lng, "label.target-character-same-hex-empty-self"), CharOpts(ctx.Game.Nations
                    .SelectMany(n => n.Characters).Where(c => !c.IsDead && c.LocationHex == ctx.EffLoc)), req: false) },
            705 => new() { Sel("spellId", OrderTexts.Get(lng, "label.spell-empty-random-research"), SpellCatalog.All
                .Where(s => !ch.Spells.Any(x => x.SpellId == s.Id && x.IsKnown && !x.IsLost)
                    && (!s.IsLost || NationAbilities.CanLearnLostSpell(ctx.Nation.Name, s.Id)))
                .Select(s => new OrderFieldOptionDto(s.Id.ToString(), $"#{s.Id} {SpellDisplayName(lng, s.Id, s.Name)}{(s.IsLost ? OrderTexts.Get(lng, "opt.lost") : "")}")).ToList(), req: false) },
            230 or 235 => new() { Sel("tactic", OrderTexts.Get(lng, "label.tactic"), TacticOptions, req: false) },
            270 or 340 or 345 or 347 or 440 or 452 or 456
                => new() { Num("amount", OrderTexts.Get(lng, "label.amount"), min: 1) },
            240 or 250 or 255 or 260 => new() { Sel("tactic", OrderTexts.Get(lng, "label.tactic"), TacticOptions, req: false) },
            275 => new() { Num("warships", OrderTexts.Get(lng, "label.warships-empty-all"), req: false, min: 0), Num("transports", OrderTexts.Get(lng, "label.transports-empty-all"), req: false, min: 0) },
            280 => new() { Num("warships", OrderTexts.Get(lng, "label.warships-empty-all"), req: false, min: 0), Num("transports", OrderTexts.Get(lng, "label.transports-empty-all"), req: false, min: 0) },
            300 => new() { Num("newRate", OrderTexts.Get(lng, "label.new-tax-rate"), min: 10, max: 80) },
            347 or 349 or 351 or 353 or 355 => new() { Sel("destArmyId", OrderTexts.Get(lng, "label.destination-army"), ArmyOpts()), Num("amount", OrderTexts.Get(lng, "label.amount"), min: 1) },
            370 or 375 => new() { Sel("material", OrderTexts.Get(lng, "label.material"), MaterialOptions(lng)),
                Num("hc", OrderTexts.Get(lng, "label.heavy-cavalry"), req: false, min: 0), Num("lc", OrderTexts.Get(lng, "label.light-cavalry"), req: false, min: 0),
                Num("hi", OrderTexts.Get(lng, "label.heavy-infantry"), req: false, min: 0), Num("li", OrderTexts.Get(lng, "label.light-infantry"), req: false, min: 0),
                Num("ar", OrderTexts.Get(lng, "opt.archers"), req: false, min: 0), Num("ma", OrderTexts.Get(lng, "label.men-at-arms"), req: false, min: 0) },
            400 or 404 or 408 or 412 => new() { Num("amount", OrderTexts.Get(lng, "label.troops"), min: 1),
                Sel("weapons", OrderTexts.Get(lng, "label.weapon-material-bronze-steel-mithril"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()), Sel("armour", OrderTexts.Get(lng, "label.armour-material"), MaterialOptions(lng)) },
            416 or 420 => new() { Num("amount", OrderTexts.Get(lng, "label.troops"), min: 1) },
            425 => new() { Num("hc", OrderTexts.Get(lng, "label.heavy-cavalry"), req: false, min: 0), Num("lc", OrderTexts.Get(lng, "label.light-cavalry"), req: false, min: 0),
                Num("hi", OrderTexts.Get(lng, "label.heavy-infantry"), req: false, min: 0), Num("li", OrderTexts.Get(lng, "label.light-infantry"), req: false, min: 0),
                Num("ar", OrderTexts.Get(lng, "opt.archers"), req: false, min: 0), Num("ma", OrderTexts.Get(lng, "label.men-at-arms"), req: false, min: 0) },
            444 => new() { Num("amount", OrderTexts.Get(lng, "label.rank-points"), min: 1), Sel("material", OrderTexts.Get(lng, "label.material"), MaterialOptions(lng).Where(m => m.Value != "wood").ToList()) },
            448 => new() { Num("amount", OrderTexts.Get(lng, "label.rank-points"), min: 1), Sel("material", OrderTexts.Get(lng, "label.material"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()) },
            494 => new() { Sel("level", OrderTexts.Get(lng, "label.fort-level-empty-next"), LevelOptions(lng), req: false) },
            470 or 498 => new() { Num("amount", OrderTexts.Get(lng, "label.amount"), req: false, min: 0) },
            725 or 728 or 731 or 734 or 737 => new() { Txt("name", OrderTexts.Get(lng, "label.name-5-17-letters-capitalized")),
                Num("command", OrderTexts.Get(lng, "label.command-0-30"), req: false, min: 0, max: 30), Num("agent", OrderTexts.Get(lng, "label.agent-0-30"), req: false, min: 0, max: 30),
                Num("emissary", OrderTexts.Get(lng, "label.emissary-0-30"), req: false, min: 0, max: 30), Num("mage", OrderTexts.Get(lng, "label.mage-0-30"), req: false, min: 0, max: 30) },
            745 => new() { Txt("name", OrderTexts.Get(lng, "label.company-name")) },
            770 => new() { Txt("name", OrderTexts.Get(lng, "label.army-name")), Num("troops", OrderTexts.Get(lng, "label.troops"), min: 1),
                Sel("troopType", OrderTexts.Get(lng, "label.troop-type"), TroopTypeOptions(lng)), Sel("weapons", OrderTexts.Get(lng, "label.weapons"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()),
                Sel("armour", OrderTexts.Get(lng, "label.armour"), MaterialOptions(lng)), Num("food", OrderTexts.Get(lng, "label.food-units"), req: false, min: 0) },
            755 => new() { Sel("commanderId", OrderTexts.Get(lng, "label.company-commander"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.CompanyId != null && !c.IsDead))) },
            765 => new() { Sel("commanderId", OrderTexts.Get(lng, "label.new-commander"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId == ctx.Nation.Id && c.CommandSkill > 0 && !c.IsDead))) },
            785 => new() { Sel("commanderId", OrderTexts.Get(lng, "label.force-commander"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))) },
            780 => new() { Sel("targetId", OrderTexts.Get(lng, "label.new-commander"), CharOpts(ownChars)) },
            910 or 915 or 920 or 930 or 475 or 490 or 605 or 665 or 670 or 675 or 680 => new() { Hx("hex", OrderTexts.Get(lng, "label.hex-empty-current-location"), req: false) },
            610 or 625 or 630 or 635 or 640 or 645 or 650 or 655 or 363 => new()
                { Sel("targetId", OrderTexts.Get(lng, "label.target"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))) },
            685 => new()
                { Sel("artifactId", OrderTexts.Get(lng, "label.artifact"), _db.Artifacts.Where(a => (a.HeldByCharacterId != null && a.HeldByCharacterId != ch.Id) || (a.HeldByCharacterId == null && a.LocationHex == ctx.EffLoc)).ToList().Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId == null ? $"{ArtifactDisplayName(lng, a)} @ {(a.LocationHex ?? "?")}" : ArtifactDisplayName(lng, a))).ToList()) },
            505 => new() { Sel("targetId", OrderTexts.Get(lng, "label.target-character"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId != ctx.Nation.Id && !c.IsDead))), Num("amount", OrderTexts.Get(lng, "label.bribe-gold-min-500"), req: false, min: 500) },
            552 or 555 => new() { Txt("name", OrderTexts.Get(lng, "label.camp-name-empty-nation-pool"), req: false) },
            560 or 565 or 580 or 585 => new() { Hx("hex", OrderTexts.Get(lng, "label.hex-empty-current-location"), req: false) },
            360 => new() { Multi("artifactId", OrderTexts.Get(lng, "label.artifacts"), heldArts), Sel("targetId", OrderTexts.Get(lng, "label.to-character-same-hex"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead && !c.IsKidnapped))) },
            792 or 796 => new() { Multi("artifactId", OrderTexts.Get(lng, "label.artifacts-1-6"), heldArts) },
            700 => new() { Multi("spellId", OrderTexts.Get(lng, "label.spells-to-forget-1-6"), ch.Spells.Where(s => s.IsKnown && !s.IsLost).Select(s => new OrderFieldOptionDto(s.SpellId.ToString(), $"#{s.SpellId} {SpellDisplayName(lng, s.SpellId, SpellCatalog.Get(s.SpellId)?.Name ?? "?")}")).ToList()) },
            798 => new() { Num("amount", OrderTexts.Get(lng, "label.transports-to-pick-up"), min: 1) },
            205 or 945 => new() { Sel("artifactId", OrderTexts.Get(lng, "label.artifact"), heldArts) },
            805 => new() { Sel("artifactId", OrderTexts.Get(lng, "label.movement-artifact"), heldArts), Hx("destination", OrderTexts.Get(lng, "label.destination-hex-empty-stay"), req: false) },
            935 => new() { Sel("artifactId", OrderTexts.Get(lng, "label.artifact"), heldArts), Hx("hex", OrderTexts.Get(lng, "label.hex-to-scry-empty-here"), req: false) },
            900 => new() { Sel("artifactId", OrderTexts.Get(lng, "label.artifact-optional"), _db.Artifacts.Where(a => a.HeldByCharacterId == null).ToList().Select(a => new OrderFieldOptionDto(a.Id, $"{ArtifactDisplayName(lng, a)} @ {a.LocationHex ?? "?"}")).ToList(), req: false) },
            905 => new() { Sel("commanderId", OrderTexts.Get(lng, "label.force-commander"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))), Flag("follow", OrderTexts.Get(lng, "label.follow")), Hx("hex", OrderTexts.Get(lng, "label.hex-empty-force-location"), req: false) },
            940 => LoreRequires(),
            949 => new() { Sel("targetId", OrderTexts.Get(lng, "label.receiving-emissary-other-nation-same-hex"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.EmissarySkill > 0 && !c.IsDead))) },
            950 => new() { Sel("pcId", OrderTexts.Get(lng, "label.new-capital-major-town-city"), ctx.Nation.PopulationCentres.Where(p => p.Size == "major town" || p.Size == "city").Select(p => new OrderFieldOptionDto(p.Id, p.Name)).ToList()) },
            660 => new() { Sel("targetId", OrderTexts.Get(lng, "label.hostage"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.IsKidnapped && !c.IsDead))), Num("amount", OrderTexts.Get(lng, "label.ransom-gold-empty-1000"), req: false, min: 1) },
            500 => new() { Sel("targetId", OrderTexts.Get(lng, "label.target-character-emissary-agent-same-hex"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => (c.EmissarySkill > 0 || c.AgentSkill > 0) && !c.IsDead))) },
            810 or 820 or 830 or 850 or 860 or 870 => new() { Txt("destination", OrderTexts.Get(lng, "label.destination-hex")), Flag("evasive", OrderTexts.Get(lng, "label.evasive")) },
            825 => new() { Sel("spellId", OrderTexts.Get(lng, "label.spell"), SpellOpts(SpellType.Movement)), Txt("destination", OrderTexts.Get(lng, "label.destination-hex")) },
            310 => new() { Sel("product", OrderTexts.Get(lng, "label.product"), ProductOptions(lng)), Num("amount", OrderTexts.Get(lng, "label.amount"), min: 1), Num("price", OrderTexts.Get(lng, "label.bid-price-empty-market"), req: false, min: 1) },
            315 or 320 => new() { Sel("product", OrderTexts.Get(lng, "label.product"), ProductOptions(lng)), Num("amount", OrderTexts.Get(lng, "label.amount"), min: 1) },
            325 => new() { Sel("product", OrderTexts.Get(lng, "label.product"), ProductOptions(lng)), Num("percentage", OrderTexts.Get(lng, "label.percentage-of-stock"), min: 1, max: 100) },
            947 or 948 => new() { Sel("resource", OrderTexts.Get(lng, "label.resource"), ProductOptions(lng)), Num("amount", OrderTexts.Get(lng, "label.amount"), min: 1) },
            _ => new List<OrderFieldSpecDto>()
        };
    }

    public List<string> ValidateEstimateParams(int code, EstimateCtx ctx, List<OrderFieldSpecDto> requires)
    {
        var errors = new List<string>();
        var pars = ctx.Pars;
        var lng = ctx.Lang;
        foreach (var f in requires.Where(f => f.Required))
        {
            if (!pars.TryGetValue(f.Key, out var el) || el.ValueKind == System.Text.Json.JsonValueKind.Null
                || (el.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(el.GetString()))
                || (el.ValueKind == System.Text.Json.JsonValueKind.Array && !el.EnumerateArray().Any()))
                errors.Add(OrderTexts.Get(lng, "err.missing-info") + f.Label);
        }
        string? Str(string k) => ParamStr(pars, k);
        var ch = ctx.Ch;
        var gameChars = ctx.Game.Nations.SelectMany(n => n.Characters).ToList();
        Character? FindChar(string? id) => string.IsNullOrEmpty(id) ? null : gameChars.FirstOrDefault(c => c.Id == id);
        PopulationCentre? PcAt(string? hex) => hex == null ? null :
            ctx.Game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(p => p.LocationHex == hex);
        PopulationCentre? OwnPcAt(string? hex) => hex == null ? null :
            ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);

        if (code is 750 or 760 && ch.CompanyId == null) errors.Add(OrderTexts.Get(lng, "reason.not-in-a-company"));
        if (code == 790 && ch.ArmyId == null) errors.Add(OrderTexts.Get(lng, "reason.not-in-an-army"));
        if (code is 210 or 615 or 620)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add(OrderTexts.Get(lng, "err.target-character-not-found"));
            else
            {
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Format(lng, "err.target-not-in-the-same-hex-at-t-locationhex", t.LocationHex));
                if (code == 615 && t.NationId == ctx.Nation.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-be-of-a-different-nation"));
                if (code == 615 && t.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.target-is-a-hostage"));
                if (code == 620 && (t.IsDead || t.IsKidnapped)) errors.Add(OrderTexts.Get(lng, "err.target-cannot-be-kidnapped"));
                if (code == 620 && t.NationId == ctx.Nation.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-be-of-a-different-nation"));
            }
        }
        if (code is 625 or 630 or 635 or 640 or 645 or 650 or 655)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add(OrderTexts.Get(lng, "err.target-not-found"));
            else
            {
                if (!t.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.target-is-not-a-hostage"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-not-in-the-same-hex-at-t-locationhex"));
            }
        }
        if (code == 275 && !ctx.Nation.Navies.Any()) errors.Add(OrderTexts.Get(lng, "err.no-navy"));
        if (code == 280)
        {
            var cap = ctx.Nation.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
            if (cap == null || cap != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-your-own-capital"));
            if (!ctx.Nation.Navies.Any()) errors.Add(OrderTexts.Get(lng, "err.no-navy"));
        }
        if (code is 300 or 325)
        {
            var cap = ctx.Nation.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
            if (cap == null || cap != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-your-own-capital"));
        }
        // Prerequisite mirrors (same messages as TurnProcessor resolve).
        var n = ctx.Nation;
        var game = ctx.Game;
        string? S(string k) => ParamStr(pars, k);
        int I(string k, int def = 0) => ParamInt(pars, k, def);
        Character? FC(string? id) => FindChar(id);
        bool SameOrFriendly(string a, string b) => a == b ||
            (game.Nations.FirstOrDefault(x => x.Id == a)?.Relations.FirstOrDefault(r => r.TargetNationId == b)?.Level ?? 0) >= 1;
        bool EnemyAt(string? hex)
        {
            if (hex == null) return false;
            bool Hostile(string a, string b) => game.Nations.FirstOrDefault(x => x.Id == a)?.Relations
                .FirstOrDefault(r => r.TargetNationId == b)?.Level <= -1;
            return game.Nations
                .SelectMany(x => x.Armies.Select(a => new { a.LocationHex, NationId = x.Id })
                    .Concat(x.Navies.Select(v => new { v.LocationHex, NationId = x.Id })))
                .Any(u => u.LocationHex == hex && u.NationId != n.Id
                    && (Hostile(u.NationId, n.Id) || Hostile(n.Id, u.NationId)));
        }
        bool OnLand(string? hex)
        {
            var t = TileAt(game.Id, hex);
            return t == null || (t.Terrain != "water" && t.Terrain != "ocean");
        }
        var capHex = n.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
        var atCapital = capHex != null && capHex == ctx.EffLoc;
        bool Usable(Artifact a)
        {
            var al = (a.Alignment ?? "").ToLower();
            if (al is "" or "none" or "neutral") return true;
            if (al == "good") return n.Allegiance == "free_peoples";
            if (al == "evil") return n.Allegiance == "dark_servants";
            return true;
        }
        bool CommandsForce(Character c) => c.ArmyId != null || c.CompanyId != null
            || game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id);
        Army? SrcArmy() => S("armyId") != null
            ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
            : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
        int TroopsOf(Army a, string t) => t switch
        {
            "HeavyCavalry" => a.HeavyCavalry, "LightCavalry" => a.LightCavalry,
            "HeavyInfantry" => a.HeavyInfantry, "LightInfantry" => a.LightInfantry,
            "Archers" => a.Archers, "MenAtArms" => a.MenAtArms, _ => 0
        };
        List<string> Ids(string key)
        {
            var ids = new List<string>();
            if (!pars.TryGetValue(key, out var el)) return ids;
            void AddEl(System.Text.Json.JsonElement e)
            {
                if (e.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrEmpty(e.GetString())) ids.Add(e.GetString()!);
                else if (e.ValueKind == System.Text.Json.JsonValueKind.Number && e.TryGetInt32(out var nn)) ids.Add(nn.ToString());
            }
            if (el.ValueKind == System.Text.Json.JsonValueKind.Array) foreach (var e in el.EnumerateArray()) AddEl(e);
            else AddEl(el);
            return ids;
        }

        // 205/805/935/945: held by the character + type + alignment.
        if (code is 205 or 805 or 935 or 945)
        {
            var want = code == 205
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sword", "Weapon", "Bow", "Mace", "Scimitar", "Hammer", "Lance", "Club", "Flail", "Axe", "Bola", "Spear" }
                : code == 805 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Boots" }
                : code == 935 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mirror", "Orb", "Sphere" }
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cloak", "Robes" };
            var kind = code == 205 ? "combat" : code == 805 ? "movement" : code == 935 ? "scrying" : "hiding";
            var aid = S("artifactId");
            var a = aid == null ? null : _db.Artifacts.FirstOrDefault(x => x.Id == aid);
            if (aid != null && a == null) errors.Add(OrderTexts.Get(lng, "err.artifact-not-found"));
            else if (a != null)
            {
                if (a.HeldByCharacterId != ch.Id) errors.Add(OrderTexts.Get(lng, "err.artifact-not-held-by-character"));
                if (!want.Contains(a.Type ?? "")) errors.Add(OrderTexts.Format(lng, "err.a-name-is-not-a-kind-artifact", a.Name, kind));
                if (!Usable(a)) errors.Add(OrderTexts.Format(lng, "err.a-name-alignment-does-not-match-your-allegiance", a.Name));
            }
        }
        // 347-355: source army + dest same hex + same/friendly.
        if (code is 347 or 349 or 351 or 353 or 355)
        {
            var src = SrcArmy();
            if (src == null) errors.Add(OrderTexts.Get(lng, "err.no-source-army"));
            else
            {
                var did = S("targetArmyId") ?? S("destArmyId");
                var dest = game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.Id == did);
                if (dest == null) errors.Add(OrderTexts.Get(lng, "err.dest-army-not-found"));
                else
                {
                    if (dest.LocationHex != src.LocationHex) errors.Add(OrderTexts.Get(lng, "err.both-armies-must-be-in-the-same-hex"));
                    if (!SameOrFriendly(n.Id, dest.NationId)) errors.Add(OrderTexts.Get(lng, "err.dest-army-must-be-of-the-same-or-a-friendly-nati"));
                }
            }
        }
        if (code == 360)
        {
            if (Ids("artifactId").Count == 0) errors.Add(OrderTexts.Get(lng, "err.missing-artifactid-s"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || t.IsKidnapped)) errors.Add(OrderTexts.Get(lng, "err.target-not-found-or-is-a-hostage"));
            else if (t != null && t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.both-characters-must-be-at-the-same-location"));
        }
        if (code == 363)
        {
            var t = FC(S("targetId"));
            if (S("targetId") != null && t == null) errors.Add(OrderTexts.Get(lng, "err.target-not-found"));
            else
            {
                if (!t.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.target-is-not-a-hostage"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-must-be-at-the-same-location"));
            }
        }
        // 370/375: army + material + troop types present.
        if (code is 370 or 375)
        {
            var army = SrcArmy();
            if (army == null) errors.Add(OrderTexts.Get(lng, "err.no-army-specified"));
            else
            {
                var m = (S("material") ?? "").ToLower();
                if (m != "leather" && m != "bronze" && m != "steel" && m != "mithril")
                    errors.Add(OrderTexts.Get(lng, "err.material-must-be-leather-bronze-steel-or-mithril"));
                var pairs = new[] { ("hc", "HeavyCavalry"), ("lc", "LightCavalry"), ("hi", "HeavyInfantry"), ("li", "LightInfantry"), ("ar", "Archers"), ("ma", "MenAtArms") };
                var wanted = pairs.Where(p => I(p.Item1) > 0).Select(p => p.Item2).ToList();
                if (wanted.Count == 0) errors.Add(OrderTexts.Get(lng, "err.specify-troops-per-type-hc-lc-hi-li-ar-ma"));
                var missing = wanted.Where(t => TroopsOf(army, t) <= 0).ToList();
                if (missing.Count > 0) errors.Add(OrderTexts.Format(lng, "err.no-troops-in-army", string.Join(", ", missing), army.Name));
            }
        }
        if (code is 400 or 404 or 408 or 412)
        {
            var w = (S("weapons") ?? "").ToLower();
            if (w != "bronze" && w != "steel" && w != "mithril")
                errors.Add(OrderTexts.Get(lng, "err.weapon-material-must-be-bronze-steel-or-mithril"));
        }
        if (code == 425)
        {
            if (SrcArmy() == null) errors.Add(OrderTexts.Get(lng, "err.no-army"));
            else if (I("hc") + I("lc") + I("hi") + I("li") + I("ar") + I("ma") <= 0)
                errors.Add(OrderTexts.Get(lng, "err.no-troops-retired-amounts-per-type-hc-lc-hi-li-a"));
        }
        if (code == 444 && new[] { "leather", "bronze", "steel", "mithril" }.All(m => m != (S("material") ?? "").ToLower()))
            errors.Add(OrderTexts.Get(lng, "err.material-must-be-leather-bronze-steel-or-mithril"));
        if (code == 448 && new[] { "bronze", "steel", "mithril" }.All(m => m != (S("material") ?? "").ToLower()))
            errors.Add(OrderTexts.Get(lng, "err.material-must-be-bronze-steel-or-mithril"));
        // 520/525 influence.
        if (code == 520)
        {
            if (OwnPcAt(ctx.EffLoc) == null) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-one-of-your-population-centres"));
            if (ch.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.needs-emissary-skill"));
        }
        if (code == 525)
        {
            if (ch.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.needs-emissary-skill"));
            var pc = PcAt(ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "err.no-population-centre-here"));
            else
            {
                if (pc.NationId == n.Id) errors.Add(OrderTexts.Get(lng, "err.use-520-on-your-own-centres"));
                if (pc.IsHidden) errors.Add(OrderTexts.Get(lng, "err.no-visible-population-centre-here"));
                var back = game.Nations.FirstOrDefault(x => x.Id == pc.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == n.Id)?.Level <= -1;
                if (back) errors.Add(OrderTexts.Get(lng, "err.enemy-forces-present"));
            }
        }
        // 530/535/550/552/555 constructions.
        if (code is 530 or 535 or 550)
        {
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-one-of-your-population-centres"));
            else
            {
                if (code == 530 && (!pc.HasHarbour || pc.HasPort)) errors.Add(OrderTexts.Format(lng, "err.pc-name-needs-a-harbour-not-yet-a-port", pc.Name));
                if (code == 530 && pc.Size != "major town" && pc.Size != "city") errors.Add(OrderTexts.Get(lng, "err.only-major-towns-and-cities-can-have-ports"));
                if (code == 535 && (pc.HasHarbour || pc.HasPort)) errors.Add(OrderTexts.Format(lng, "err.pc-name-already-has-a-harbour-or-port", pc.Name));
                if (code == 535 && pc.Size != "town" && pc.Size != "major town" && pc.Size != "city") errors.Add(OrderTexts.Get(lng, "err.only-towns-and-larger-can-have-harbours"));
                if (code == 550 && (pc.Size == "city" || pc.Size == "citadel")) errors.Add(OrderTexts.Format(lng, "err.pc-name-cannot-be-improved-further", pc.Name));
            }
            if (EnemyAt(ctx.EffLoc)) errors.Add(OrderTexts.Get(lng, "err.enemy-forces-present"));
        }
        if (code is 552 or 555)
        {
            if (!OnLand(ctx.EffLoc)) errors.Add(OrderTexts.Get(lng, "err.camps-need-a-land-hex"));
            if (OwnPcAt(ctx.EffLoc) != null) errors.Add(OrderTexts.Get(lng, "err.already-have-a-population-centre-at-this-hex"));
            if (EnemyAt(ctx.EffLoc)) errors.Add(OrderTexts.Get(lng, "err.enemy-forces-present"));
        }
        if (code == 565)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "err.no-population-centre-at-location"));
            else
            {
                if (pc.NationId != n.Id) errors.Add(OrderTexts.Get(lng, "err.can-only-reduce-your-own-population-centres"));
                if (pc.Size.ToLower() == "camp") errors.Add(OrderTexts.Get(lng, "err.camps-cannot-be-reduced-further-abandon-them"));
            }
        }
        if (code == 605)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc != null && pc.NationId != n.Id && pc.IsHidden) errors.Add(OrderTexts.Get(lng, "err.no-visible-population-centre-here"));
        }
        if (code == 660)
        {
            if (!atCapital) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-your-own-capital"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || !t.IsKidnapped)) errors.Add(OrderTexts.Get(lng, "err.target-not-found-or-not-kidnapped"));
        }
        // 670/675/680 sabotage.
        if (code is 670 or 675 or 680)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "err.no-population-centre"));
            else
            {
                if (pc.NationId == n.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-be-of-a-different-nation"));
                if (pc.IsHidden) errors.Add(OrderTexts.Get(lng, "err.no-visible-population-centre-here"));
                if (code == 670 && string.IsNullOrEmpty(pc.Fortification)) errors.Add(OrderTexts.Format(lng, "err.pc-name-has-no-fortifications", pc.Name));
                if (code == 675 && !pc.HasHarbour && !pc.HasPort) errors.Add(OrderTexts.Format(lng, "err.pc-name-has-no-harbour-or-port", pc.Name));
                if (code == 680)
                {
                    var st = (S("store") ?? "timber").ToLower();
                    if (st != "timber" && st != "food" && st != "mounts" && st != "leather" && st != "bronze" && st != "steel" && st != "mithril")
                        errors.Add(OrderTexts.Get(lng, "err.store-must-be-timber-food-mounts-leather-bronze"));
                }
            }
        }
        if (code == 685)
        {
            var aid685 = S("artifactId");
            var a = aid685 == null ? null : _db.Artifacts.FirstOrDefault(x => x.Id == aid685);
            if (aid685 != null && a == null) errors.Add(OrderTexts.Get(lng, "err.artifact-not-found"));
            else if (a.HeldByCharacterId == ch.Id) errors.Add(OrderTexts.Get(lng, "err.you-already-hold-it"));
            else if (a.HeldByCharacterId != null)
            {
                var holder = gameChars.FirstOrDefault(c => c.Id == a.HeldByCharacterId);
                if (holder == null) errors.Add(OrderTexts.Get(lng, "err.holder-not-found"));
                else
                {
                    if (holder.NationId == n.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-belong-to-another-nation"));
                    if (holder.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.artifact-must-be-at-the-same-hex"));
                }
            }
            else
            {
                if (a.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.artifact-must-be-at-the-same-hex"));
                else
                {
                    var pc = PcAt(a.LocationHex);
                    if (pc != null && (pc.IsHidden || pc.NationId == n.Id))
                        errors.Add(OrderTexts.Get(lng, "err.artifact-must-lie-in-a-visible-foreign-populatio"));
                }
            }
        }
        if (code == 690)
        {
            var victim = game.Nations.FirstOrDefault(x => x.Id == S("nationId"));
            if (victim == null) errors.Add(OrderTexts.Get(lng, "err.victim-nation-not-found"));
            else
            {
                if (victim.Id == n.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-be-of-a-different-nation"));
                var pc = game.Nations.SelectMany(x => x.PopulationCentres)
                    .FirstOrDefault(x => x.LocationHex == ctx.EffLoc && x.NationId == victim.Id);
                if (pc == null || pc.IsHidden) errors.Add(OrderTexts.Get(lng, "err.need-a-visible-foreign-population-centre-at-your"));
            }
        }
        if (code is 792 or 796)
        {
            var c = Ids("artifactId").Count;
            if (c < 1 || c > 6) errors.Add(OrderTexts.Get(lng, "err.give-1-6-artifactid-s"));
        }
        if (code == 700)
        {
            var c = Ids("spellId").Count;
            if (c < 1 || c > 6) errors.Add(OrderTexts.Get(lng, "err.give-1-6-spellid-s-to-forget"));
        }
        if (code == 705)
        {
            if (OwnPcAt(ctx.EffLoc) == null) errors.Add(OrderTexts.Get(lng, "err.must-be-at-one-of-your-population-centres-to-res"));
            if (ch.Spells.Count(s => s.IsKnown && !s.IsLost) >= 15) errors.Add(OrderTexts.Get(lng, "err.already-knows-15-spells"));
            if (pars.TryGetValue("spellId", out var rsEl) && rsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var sd = SpellCatalog.Get(rsEl.GetInt32());
                if (sd == null) errors.Add(OrderTexts.Get(lng, "err.unknown-spell"));
                else if (sd.IsLost && !NationAbilities.CanLearnLostSpell(n.Name, sd.Id))
                    errors.Add(OrderTexts.Format(lng, "err.lost-spell-sd-name-is-not-available-to-your-nati", sd.Name));
            }
        }
        if (code == 710 && OwnPcAt(ctx.EffLoc) == null)
            errors.Add(OrderTexts.Get(lng, "err.must-be-at-one-of-your-population-centres-to-tra"));
        if (code is 725 or 728 or 731 or 734 or 737)
        {
            var nm = (S("name") ?? "").Trim();
            if (nm.Length < 5 || nm.Length > 17) errors.Add(OrderTexts.Get(lng, "err.name-must-be-5-17-letters"));
            if (!atCapital) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-your-own-capital"));
        }
        if (code == 740 && ch.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.cannot-retire-a-hostage"));
        if (code == 745)
        {
            if (CommandsForce(ch)) errors.Add(OrderTexts.Get(lng, "err.character-already-commands-a-force"));
            if (!OnLand(ctx.EffLoc)) errors.Add(OrderTexts.Get(lng, "err.must-be-on-land"));
        }
        if (code == 755)
        {
            var c = FC(S("commanderId"));
            if (S("commanderId") != null && (c == null || c.CompanyId == null)) errors.Add(OrderTexts.Get(lng, "err.commander-has-no-company"));
            else
            {
                var comp = _db.Companies.Find(c.CompanyId);
                if (comp == null) errors.Add(OrderTexts.Get(lng, "err.company-not-found"));
                else
                {
                    if (_db.Characters.Count(x => x.CompanyId == comp.Id && !x.IsDead) >= 9)
                        errors.Add(OrderTexts.Format(lng, "err.company-comp-name-is-full-9-members", comp.Name));
                    if (comp.NationId != n.Id && !SameOrFriendly(n.Id, comp.NationId))
                        errors.Add(OrderTexts.Get(lng, "err.company-must-be-of-the-same-or-a-friendly-nation"));
                }
            }
        }
        if (code == 765)
        {
            var army = SrcArmy();
            if (army == null) errors.Add(OrderTexts.Get(lng, "err.no-army-to-split"));
            var c = FC(S("commanderId"));
            if (S("commanderId") != null && (c == null || c.IsDead)) errors.Add(OrderTexts.Get(lng, "err.new-commander-not-found"));
            else
            {
                if (c.NationId != n.Id) errors.Add(OrderTexts.Get(lng, "err.new-commander-must-be-of-the-same-nation"));
                if (c.CommandSkill <= 0) errors.Add(OrderTexts.Get(lng, "err.new-commander-needs-command-skill"));
                if (c.ArmyId != null || c.CompanyId != null
                    || game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id))
                    errors.Add(OrderTexts.Format(lng, "err.c-name-already-commands-a-force", c.Name));
                if (army != null && c.LocationHex != army.LocationHex)
                    errors.Add(OrderTexts.Get(lng, "err.new-commander-must-be-at-the-same-hex"));
            }
        }
        if (code == 770)
        {
            if (CommandsForce(ch)) errors.Add(OrderTexts.Get(lng, "err.character-already-commands-a-force"));
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null || pc.IsSieged) errors.Add(OrderTexts.Get(lng, "err.must-be-at-one-of-your-non-sieged-population-cen"));
            var tt = S("troopType") ?? "MenAtArms";
            var shorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" }, { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" } };
            if (shorts.TryGetValue(tt, out var fullT)) tt = fullT;
            if (tt != "HeavyCavalry" && tt != "LightCavalry" && tt != "HeavyInfantry" && tt != "LightInfantry" && tt != "Archers" && tt != "MenAtArms")
                errors.Add(OrderTexts.Get(lng, "err.troop-type-must-be-hc-lc-hi-li-ar-or-ma"));
            var w = (S("weapons") ?? "bronze").ToLower();
            if (w != "bronze" && w != "steel" && w != "mithril") errors.Add(OrderTexts.Get(lng, "err.weapon-material-must-be-bronze-steel-or-mithril"));
            var ar2 = (S("armour") ?? "leather").ToLower();
            if (ar2 != "leather" && ar2 != "bronze" && ar2 != "steel" && ar2 != "mithril")
                errors.Add(OrderTexts.Get(lng, "err.armour-material-must-be-leather-bronze-steel-or"));
        }
        if (code == 775)
        {
            var army = S("armyId") != null
                ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
                : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
            if (army == null) errors.Add(OrderTexts.Get(lng, "err.no-army-to-disband"));
        }
        if (code == 780)
        {
            var army = S("armyId") != null
                ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
                : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
            if (army == null) errors.Add(OrderTexts.Get(lng, "err.no-army-to-transfer-command-of"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && t == null) errors.Add(OrderTexts.Get(lng, "err.target-commander-not-found"));
            else if (t != null)
            {
                if (t.NationId != n.Id) errors.Add(OrderTexts.Get(lng, "err.new-commander-must-be-of-the-same-nation"));
                if (t.CommandSkill <= 0) errors.Add(OrderTexts.Get(lng, "err.new-commander-needs-command-skill"));
            }
        }
        if (code == 785)
        {
            if (CommandsForce(ch)) errors.Add(OrderTexts.Get(lng, "err.character-already-commands-a-force"));
            var boss = FC(S("commanderId"));
            if (S("commanderId") != null && (boss == null || boss.IsDead)) errors.Add(OrderTexts.Get(lng, "err.commander-not-found"));
            else
            {
                if (boss.NationId != n.Id) errors.Add(OrderTexts.Get(lng, "err.can-only-join-a-force-of-your-own-nation"));
                var fa = game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
                var fv = fa == null ? game.Nations.SelectMany(x => x.Navies).FirstOrDefault(v => v.CommanderId == boss.Id) : null;
                if (fa == null && fv == null) errors.Add(OrderTexts.Format(lng, "err.boss-name-commands-no-force", boss.Name));
                else if ((fa?.LocationHex ?? fv!.LocationHex) != ctx.EffLoc)
                    errors.Add(OrderTexts.Get(lng, "err.force-to-join-must-be-at-the-same-hex"));
            }
        }
        if (code == 905)
        {
            var boss = FC(S("commanderId"));
            if (S("commanderId") != null && (boss == null || boss.IsDead)) errors.Add(OrderTexts.Get(lng, "err.commander-not-found"));
            else
            {
                var fa = game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id);
                var fv = game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id);
                if (!fa && !fv) errors.Add(OrderTexts.Get(lng, "err.boss-name-commands-no-force"));
            }
        }
        if (code is 910 or 915 or 925 or 930 && !OnLand(ctx.EffLoc))
            errors.Add(OrderTexts.Get(lng, "err.must-be-on-land"));
        if (code == 940)
        {
            if (S("targetId") is { } t1 && FC(t1) == null) errors.Add(OrderTexts.Get(lng, "err.target-character-not-found"));
            if (S("nationId") is { } n1 && !game.Nations.Any(x => x.Id == n1)) errors.Add(OrderTexts.Get(lng, "err.nation-not-found"));
            if (S("artifactId") is { } a1 && !_db.Artifacts.Any(x => x.Id == a1)) errors.Add(OrderTexts.Get(lng, "err.artifact-not-found"));
            if (S("commanderId") is { } c1)
            {
                var boss = FC(c1);
                if (boss == null || boss.IsDead) errors.Add(OrderTexts.Get(lng, "err.commander-not-found"));
                else if (!game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id)
                    && !game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id))
                    errors.Add(OrderTexts.Get(lng, "err.boss-name-commands-no-force"));
            }
            if (S("allegiance") is { } al && al != "free_peoples" && al != "dark_servants" && al != "neutral")
                errors.Add(OrderTexts.Get(lng, "err.allegiance-must-be-free-peoples-dark-servants-or"));
        }
        if (code == 949)
        {
            if (ch.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.needs-emissary-skill"));
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "err.no-population-centre"));
            else
            {
                if (pc.IsHidden) errors.Add(OrderTexts.Get(lng, "err.population-centre-is-hidden"));
                if (pc.IsCapital) errors.Add(OrderTexts.Get(lng, "err.cannot-transfer-the-capital"));
            }
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || t.IsDead)) errors.Add(OrderTexts.Get(lng, "err.target-not-found"));
            else if (t != null)
            {
                if (t.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.target-is-a-hostage"));
                if (t.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.target-needs-emissary-skill"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-must-be-at-the-same-location"));
                if (t.NationId == n.Id) errors.Add(OrderTexts.Get(lng, "err.target-must-be-of-another-nation"));
                var fwd = n.Relations.FirstOrDefault(r => r.TargetNationId == t.NationId);
                if ((fwd?.Level ?? 0) <= -1) errors.Add(OrderTexts.Get(lng, "err.nations-are-enemies"));
            }
        }
        if (code == 950)
        {
            if (ctx.EffLoc != capHex) errors.Add(OrderTexts.Get(lng, "reason.must-be-at-your-current-capital"));
            PopulationCentre? pc = S("pcId") != null
                ? game.Nations.SelectMany(x => x.PopulationCentres).FirstOrDefault(p => p.Id == S("pcId"))
                : PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add("No population centre");
            else
            {
                if (pc.NationId != n.Id) errors.Add("New capital must be owned by your nation");
                if (pc.IsSieged) errors.Add("Capital and new capital must not be under siege");
                var cur = n.PopulationCentres.FirstOrDefault(x => x.IsCapital);
                if (cur != null && cur.IsSieged) errors.Add(OrderTexts.Get(lng, "err.capital-and-new-capital-must-not-be-under-siege"));
                if (pc.Size != "major town" && pc.Size != "city") errors.Add(OrderTexts.Get(lng, "err.new-capital-must-be-a-major-town-or-city"));
            }
        }
        if (code == 990)
        {
            if (n.Allegiance == "neutral") errors.Add(OrderTexts.Get(lng, "err.neutral-nations-cannot-wield-the-one-ring"));
            if (CommandsForce(ch)) errors.Add(OrderTexts.Get(lng, "err.bearer-must-travel-alone-no-army-company-or-navy"));
            if ((ctx.EffLoc ?? "") != "34,23") errors.Add(OrderTexts.Get(lng, "err.the-bearer-must-be-at-mount-doom-34-23"));
            var ring = _db.Artifacts.FirstOrDefault(a => a.HeldByCharacterId == ch.Id
                && (a.Id == "14" || (a.Name ?? "").Contains("One Ring", StringComparison.OrdinalIgnoreCase)));
            if (ring == null) errors.Add(OrderTexts.Get(lng, "err.bearer-must-possess-artifact-14-the-one-ring"));
        }
        if (code is 685)
        {
            var a = Str("artifactId");
            if (a != null && !_db.Artifacts.Any(x => x.Id == a)) errors.Add(OrderTexts.Get(lng, "err.artifact-not-found"));
        }
        if (code is 690)
        {
            if (!ctx.Game.Nations.Any(n => n.Id == Str("nationId"))) errors.Add(OrderTexts.Get(lng, "err.victim-nation-not-found"));
        }
        if (code is 180 or 185 && !ctx.Game.Nations.Any(n => n.Id == Str("nationId")))
            errors.Add(OrderTexts.Get(lng, "err.nation-not-found"));
        if (code == 175)
        {
            var al = (Str("allegiance") ?? "").ToLower();
            if (al != "free_peoples" && al != "dark_servants" && al != "neutral")
                errors.Add(OrderTexts.Get(lng, "err.allegiance-must-be-free-peoples-dark-servants-or"));
        }
        if (code is 120 or 330 or 940 or 225 or 825)
        {
            var want = code == 120 ? SpellType.Heal : code == 330 ? SpellType.Conjuring
                : code == 940 ? SpellType.Lore : code == 825 ? SpellType.Movement : SpellType.Combat;
            var sid = ParamInt(pars, "spellId", -1);
            var def = SpellCatalog.Get(sid);
            if (def == null || def.Type != want || def.IsLost
                || !ch.Spells.Any(s => s.SpellId == sid && s.IsKnown && !s.IsLost))
                errors.Add(OrderTexts.Format(lng, "err.valid-spell", OrderTexts.SpellTypeName(lng, want)));
        }
        if (code is 700 or 705 && pars.TryGetValue("spellId", out var sidEl)
            && sidEl.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            var sd = SpellCatalog.Get(sidEl.GetInt32());
            if (sd == null) errors.Add(OrderTexts.Get(lng, "err.unknown-spell"));
            else if (code == 705 && sd.IsLost && !NationAbilities.CanLearnLostSpell(ctx.Nation.Name, sd.Id))
                errors.Add(OrderTexts.Get(lng, "err.lost-spell-sd-name-is-not-available-to-your-nati"));
        }
        if (code is 755 && Str("companyId") != null)
        {
            var cid = Str("companyId");
            if (!_db.Companies.Any(c => c.Id == cid && c.NationId == ctx.Nation.Id))
                errors.Add(OrderTexts.Get(lng, "err.company-not-found"));
        }
        if (code == 560 && !ctx.Nation.PopulationCentres.Any(p => p.LocationHex == ctx.EffLoc && p.Size == "camp"))
            errors.Add(OrderTexts.Get(lng, "err.no-camp-of-yours-here"));
        if (code is 310 or 315 or 320 or 325)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out _))
                errors.Add(OrderTexts.Get(lng, "err.product-must-be-timber-leather-bronze-steel-mith"));
        }
        if (code is 947 or 948)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "resource"), out _))
                errors.Add(OrderTexts.Get(lng, "err.resource-must-be-timber-leather-bronze-steel-mit"));
        }
        if (code is 475 or 490 or 665)
        {
            var tile = TileAt(gameId: ctx.Game.Id, hex: ParamStr(pars, "hex") ?? ctx.EffLoc ?? "");
            if (tile == null) errors.Add(OrderTexts.Get(lng, "err.no-such-hex"));
            else if (code == 475 && !tile.HasBridge) errors.Add(OrderTexts.Format(lng, "err.no-bridge-at-tile-q-tile-r", tile.Q, tile.R));
            else if (code == 665 && !tile.HasBridge) errors.Add(OrderTexts.Get(lng, "err.no-bridge-at-tile-q-tile-r"));
            else if (code == 490 && !tile.HasMajorRiver && !tile.HasMinorRiver) errors.Add(OrderTexts.Get(lng, "err.no-river-here"));
            else if (code == 490 && (tile.HasBridge || tile.HasFord)) errors.Add(OrderTexts.Get(lng, "err.a-ford-or-bridge-already-exists-here"));
        }
        if (code == 494)
        {
            var pc = ParamStr(pars, "pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                : OwnPcAt(ParamStr(pars, "hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(OrderTexts.Get(lng, "err.must-target-one-of-your-population-centres"));
        }
        if (code == 500)
        {
            if (ch.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.needs-emissary-skill"));
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && (t == null || t.IsDead)) errors.Add(OrderTexts.Get(lng, "err.target-character-not-found"));
            else if (t != null)
            {
                if (t.EmissarySkill <= 0 && t.AgentSkill <= 0) errors.Add(OrderTexts.Get(lng, "err.target-must-have-emissary-or-agent-skill"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-not-in-the-same-hex-at-t-locationhex"));
                if (t.NationId == ctx.Nation.Id) errors.Add(OrderTexts.Get(lng, "err.cannot-plant-a-double-agent-in-your-own-nation"));
            }
        }
        if (code == 505)
        {
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && t == null) errors.Add(OrderTexts.Get(lng, "err.target-not-found"));
            else if (t != null)
            {
                if (t.NationId == ctx.Nation.Id) errors.Add(OrderTexts.Get(lng, "err.cannot-bribe-your-own-character"));
                if (t.IsKidnapped) errors.Add(OrderTexts.Get(lng, "err.cannot-bribe-a-hostage"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-not-in-the-same-hex-at-t-locationhex"));
            }
            if (ch.EmissarySkill <= 0) errors.Add(OrderTexts.Get(lng, "err.needs-emissary-skill"));
        }
        if (code == 610)
        {
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && (t == null || t.IsDead)) errors.Add(OrderTexts.Get(lng, "err.target-not-found"));
            else if (t != null)
            {
                if (t.Id == ch.Id) errors.Add(OrderTexts.Get(lng, "err.cannot-guard-yourself"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(OrderTexts.Get(lng, "err.target-not-in-the-same-hex-at-t-locationhex"));
            }
        }
        return errors;
    }

    private static Dictionary<string, System.Text.Json.JsonElement> ParseStoredParams(string? json)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (string.IsNullOrWhiteSpace(json)) return d;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject()) d[p.Name] = p.Value.Clone();
        }
        catch { }
        return d;
    }

    private async Task<List<Order>> PendingNationOrders(string gameId, string nationId)
    {
        var turn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();
        if (turn == null) return new List<Order>();
        return await _db.Orders
            .Where(o => o.GameId == gameId && o.TurnId == turn.Id && o.NationId == nationId && o.Status == "pending")
            .ToListAsync();
    }

    private PendingUsage SummarizePendingUsage(List<Order> pending, Nation nation, Game game)
    {
        var used = new PendingUsage();
        var chars = game.Nations.SelectMany(n => n.Characters).ToDictionary(c => c.Id);
        foreach (var o in pending)
        {
            var p = ParseStoredParams(o.Parameters);
            int Amt(string k, int def = 0)
            {
                if (p.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n2)) return n2;
                return def;
            }
            string? Str(string k) => p.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : null;
            if (o.Code is >= 400 and <= 420)
            {
                var amount = Amt("amount");
                if (amount <= 0) continue;
                chars.TryGetValue(o.CharacterId, out var chx);
                var ax = o.ArmyId != null ? nation.Armies.FirstOrDefault(a => a.Id == o.ArmyId)
                    : chx?.ArmyId != null ? nation.Armies.FirstOrDefault(a => a.Id == chx.ArmyId)
                    : nation.Armies.FirstOrDefault();
                var pc = ax == null ? null : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == ax.LocationHex);
                if (pc != null) used.RecruitsByPc[pc.Id] = used.RecruitsByPc.GetValueOrDefault(pc.Id) + amount;
            }
            else if (o.Code is 310 or 315)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (prod != "") used.BuyByProduct[prod] = used.BuyByProduct.GetValueOrDefault(prod) + Amt("amount");
            }
            else if (o.Code is 320)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _) && Amt("amount") > 0)
                {
                    var (_, baseSell) = TurnProcessor.MarketRate(prod);
                    used.SellGoldUsed += Amt("amount") * NationAbilities.MarketSellPrice(nation.Name, baseSell);
                }
            }
            else if (o.Code is 325)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _))
                {
                    var (_, baseSellAll) = TurnProcessor.MarketRate(prod);
                    var sell = NationAbilities.MarketSellPrice(nation.Name, baseSellAll);
                    var pct = Amt("percentage", 100);
                    var stock = StockOfLocal(nation, prod);
                    used.SellGoldUsed += stock * pct / 100 * sell;
                }
            }
            else if (o.Code == 494)
            {
                var pc = Str("pcId") != null
                    ? nation.PopulationCentres.FirstOrDefault(x => x.Id == Str("pcId"))
                    : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == (Str("hex") ?? chars.GetValueOrDefault(o.CharacterId)?.LocationHex));
                if (pc != null) used.FortifiedPcs.Add(pc.Id);
            }
            else if (o.Code == 500 && Str("targetNationId") is { } tn) used.DoubleAgentNations.Add(tn);
            else if (o.Code == 210 && Str("targetId") is { } tg) used.ChallengedTargets.Add(tg);
            else if (o.Code == 505 && Str("targetId") is { } tb) used.BribedTargets.Add(tb);
            else if ((o.Code == 360 || o.Code == 792) && Str("artifactId") is { } ar) used.MovedArtifacts.Add(ar);
        }
        return used;
    }

    private static int StockOfLocal(Nation n, string p) => p switch
    {
        "timber" => n.Timber, "leather" => n.Leather, "bronze" => n.Bronze,
        "steel" => n.Steel, "mithril" => n.Mithril, "mounts" => n.Mounts, "food" => n.Food, _ => 0
    };

    public void CrossOrderConflicts(int code, EstimateCtx ctx, List<Order> pending,
        PendingUsage used, List<string> errors, List<string> warnings)
    {
        var pars = ctx.Pars;
        var lng = ctx.Lang;
        string? Str(string k) => ParamStr(pars, k);
        if (code == 494)
        {
            var pc = Str("pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == Str("pcId"))
                : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == (Str("hex") ?? ctx.EffLoc));
            if (pc != null && used.FortifiedPcs.Contains(pc.Id))
                errors.Add(OrderTexts.Format(lng, "err.pc-name-is-already-fortified-by-another-order-th", pc.Name));
        }
        if (code == 500 && Str("targetNationId") is { } tn && used.DoubleAgentNations.Contains(tn))
            errors.Add(OrderTexts.Get(lng, "err.another-order-already-recruits-a-double-agent-th"));
        if (code == 210 && Str("targetId") is { } tg && used.ChallengedTargets.Contains(tg))
        {
            var otherMax = 0;
            foreach (var o in pending.Where(o => o.Code == 210))
            {
                var pp = ParseStoredParams(o.Parameters);
                if (pp.TryGetValue("targetId", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String && t.GetString() == tg)
                {
                    var oc = ctx.Game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == o.CharacterId);
                    if (oc != null) otherMax = Math.Max(otherMax, oc.ChallengeRank);
                }
            }
            if (ctx.Ch.ChallengeRank < otherMax)
                errors.Add(OrderTexts.Format(lng, "err.target-already-challenged-by-higher-challenge-ra", otherMax, ctx.Ch.ChallengeRank));
            else
                warnings.Add(OrderTexts.Get(lng, "warn.target-already-challenged-by-another-character-o"));
        }
        if (code == 505 && Str("targetId") is { } tb && used.BribedTargets.Contains(tb))
            warnings.Add(OrderTexts.Get(lng, "warn.target-already-bribed-by-another-character-first"));
        if ((code == 360 || code == 792) && ParamStr(pars, "artifactId") is { } ar && used.MovedArtifacts.Contains(ar))
            warnings.Add(OrderTexts.Get(lng, "warn.artifact-already-moved-by-another-pending-order"));
        if ((code is 320 or 325) && used.SellGoldUsed > 0)
            warnings.Add(OrderTexts.Format(lng, "warn.used-sellgoldused-gold-of-sell-cap-already-used", used.SellGoldUsed));
        if (code is 400 or 404 or 408 or 412 or 416 or 420)
        {
            var army = ParamStr(pars, "armyId") != null
                ? ctx.Nation.Armies.FirstOrDefault(a => a.Id == ParamStr(pars, "armyId"))
                : ctx.Nation.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId);
            var pc = army == null ? null : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == army.LocationHex);
            if (pc != null)
            {
                var left = TurnProcessor.RecruitCapacity(pc.Size) - used.RecruitsByPc.GetValueOrDefault(pc.Id);
                var want = ParamInt(pars, "amount", left);
                if (want > left)
                    errors.Add(OrderTexts.Format(lng, "err.only-math-max-0-left-recruits-left-at-pc-name-ot", Math.Max(0, left), pc.Name, used.RecruitsByPc.GetValueOrDefault(pc.Id)));
            }
        }
    }

    public object EstimateCosts(int code, EstimateCtx ctx, out int? maxAmount, out int? expectedGold, PendingUsage? used = null)
    {
        maxAmount = null;
        expectedGold = null;
        var costs = new Dictionary<string, int>();
        var n = ctx.Nation;
        var pars = ctx.Pars;
        Army? Army() => ParamStr(pars, "armyId") != null
            ? n.Armies.FirstOrDefault(a => a.Id == ParamStr(pars, "armyId"))
            : n.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId);
        PopulationCentre? OwnPc(string? hex) =>
            n.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);
        int TotalTroops(Army a) => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry
            + a.LightInfantry + a.Archers + a.MenAtArms;

        switch (code)
        {
            case 400 or 404 or 408 or 412 or 416 or 420:
            {
                var army = Army();
                if (army == null) break;
                var pc = OwnPc(army.LocationHex);
                if (pc == null) break;
                var unit = TurnProcessor.RecruitCostPerUnit[code];
                var cap = TurnProcessor.RecruitCapacity(pc.Size) - (used?.RecruitsByPc.GetValueOrDefault(pc.Id) ?? 0);
                var max = Math.Min(cap, n.Gold / unit);
                if (code is 400 or 404) max = Math.Min(max, n.Mounts);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * unit;
                if (code is 400 or 404) costs["mounts"] = amount;
                break;
            }
            case 452 or 456:
            {
                var army = Army();
                var pc = army == null ? null : OwnPc(army.LocationHex);
                if (pc == null || (!pc.HasPort && !pc.HasHarbour)) break;
                var timberEach = NationAbilities.ShipTimberCost(n.Name);
                var max = Math.Min(n.Timber / timberEach, n.Gold / 1000);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 1000;
                costs["timber"] = amount * timberEach;
                break;
            }
            case 440:
            {
                if (Army() == null || OwnPc(Army()!.LocationHex) == null) break;
                var max = Math.Min(n.Gold / 50, Math.Min(n.Timber / 10, n.Steel / 5));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 50;
                costs["timber"] = amount * 10;
                costs["steel"] = amount * 5;
                break;
            }
            case 444:
            {
                var army = Army();
                if (army == null || OwnPc(army.LocationHex) == null) break;
                var headroom = Math.Max(0, 100 - army.HCArmourRank);
                var max = Math.Min(headroom, Math.Min(n.Gold / 5, Math.Min(n.Leather / 5, n.Steel / 2)));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 5;
                costs["leather"] = amount * 5;
                costs["steel"] = amount * 2;
                break;
            }
            case 448:
            {
                var army = Army();
                if (army == null || OwnPc(army.LocationHex) == null) break;
                var headroom = Math.Max(0, 100 - army.HCWeaponRank);
                var max = Math.Min(headroom, Math.Min(n.Gold / 5, Math.Min(n.Bronze / 3, n.Steel / 1)));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 5;
                costs["bronze"] = amount * 3;
                costs["steel"] = amount;
                break;
            }
            case 370 or 375:
            {
                if (Army() == null) break;
                var material = (ParamStr(pars, "material") ?? "bronze").ToLower();
                var present = TotalTroops(Army()!);
                maxAmount = present;
                var asked = ParamInt(pars, "hc") + ParamInt(pars, "lc") + ParamInt(pars, "hi")
                    + ParamInt(pars, "li") + ParamInt(pars, "ar") + ParamInt(pars, "ma");
                var amount = Math.Min(asked <= 0 ? present : asked, present);
                costs["gold"] = code == 370 ? 500 : 600;
                costs[material] = Math.Max(1, (amount + 99) / 100);
                break;
            }
            case 494:
            {
                var pc = ParamStr(pars, "pcId") != null
                    ? n.PopulationCentres.FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                    : OwnPc(ParamStr(pars, "hex") ?? ctx.EffLoc);
                if (pc == null) break;
                var ladder = new[] { "tower", "fort", "castle", "keep", "citadel" };
                var cur = FortLadderIndexLocal(pc.Fortification);
                if (cur + 1 > 4) break;
                var fort = ladder[cur + 1];
                costs["timber"] = NationAbilities.FortTimberCost(n.Name, fort);
                costs["gold"] = NationAbilities.FortGoldCost(fort);
                break;
            }
            case 530: costs["gold"] = 1500; costs["timber"] = 2500; break;
            case 535: costs["gold"] = 2500; costs["timber"] = 5000; break;
            case 555: costs["gold"] = 2000; break;
            case 552: costs["gold"] = 4000; break;
            case 745: costs["gold"] = 500; break;
            case 770:
            {
                costs["gold"] = NationAbilities.HireArmyCost(n.Name);
                var tnum = Math.Max(0, ParamInt(pars, "troops", 0));
                if (tnum > 0)
                {
                    var ttype = (ParamStr(pars, "troopType") ?? "MenAtArms");
                    var sh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        { { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" }, { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" } };
                    if (sh.TryGetValue(ttype, out var fullT2)) ttype = fullT2;
                    var matU = Math.Max(1, (tnum + 99) / 100);
                    var food = Math.Max(0, ParamInt(pars, "food", 0));
                    if (food > 0) costs["food"] = food;
                    if (ttype is "HeavyCavalry" or "LightCavalry") costs["mounts"] = tnum;
                    costs[(ParamStr(pars, "weapons") ?? "bronze").ToLower()] = matU;
                    costs[(ParamStr(pars, "armour") ?? "leather").ToLower()] = matU;
                }
                break;
            }
            case 950: costs["gold"] = 25000; break;
            case 505: costs["gold"] = Math.Max(500, ParamInt(pars, "amount", 500)); break;
            case 660: costs["gold"] = Math.Max(1000, ParamInt(pars, "amount", 1000)); break;
            case 725: costs["gold"] = 10000; break;
            case 728 or 731 or 734 or 737: costs["gold"] = 5000; break;
            case 270 or 275:
            {
                var navy = n.Navies.FirstOrDefault();
                if (navy != null) maxAmount = code == 270 ? navy.Warships : navy.Transports;
                break;
            }
            case 425:
            {
                var army = Army();
                if (army == null) break;
                maxAmount = TotalTroops(army);
                var amount = Math.Min(ParamInt(pars, "amount", maxAmount.Value), maxAmount.Value);
                expectedGold = amount * 10;
                break;
            }
            case 470:
            {
                var pc = OwnPc(ctx.EffLoc);
                if (pc == null) break;
                maxAmount = pc.Stores;
                break;
            }
            case 310 or 315:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (baseBuy, _) = TurnProcessor.MarketRate(product);
                var buy = code == 310
                    ? Math.Max(NationAbilities.MarketBuyPrice(n.Name, baseBuy), ParamInt(pars, "price", NationAbilities.MarketBuyPrice(n.Name, baseBuy)))
                    : NationAbilities.MarketBuyPrice(n.Name, baseBuy);
                var poolLeft = Math.Max(0, TurnProcessor.MarketBuyPoolFor(product) - (used?.BuyByProduct.GetValueOrDefault(product) ?? 0));
                var max = Math.Min(n.Gold / Math.Max(1, buy), poolLeft);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * buy;
                break;
            }
            case 320:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (_, baseSell) = TurnProcessor.MarketRate(product);
                var sell = NationAbilities.MarketSellPrice(n.Name, baseSell);
                var stock = StockOf(n, product);
                maxAmount = stock;
                var amount = Math.Min(ParamInt(pars, "amount", stock), stock);
                costs[product] = amount;
                var remaining = TurnProcessor.MarketSellCapGold() - (used?.SellGoldUsed ?? 0);
                expectedGold = Math.Max(0, Math.Min(amount * sell, remaining));
                break;
            }
            case 325:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (_, baseSellAll) = TurnProcessor.MarketRate(product);
                var sell = NationAbilities.MarketSellPrice(n.Name, baseSellAll);
                var stock = StockOf(n, product);
                maxAmount = stock;
                var pct = pars.TryGetValue("percentage", out var pe) && pe.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? Math.Clamp(pe.GetInt32(), 0, 100) : 100;
                var amount = stock * pct / 100;
                costs[product] = amount;
                var remainingAll = TurnProcessor.MarketSellCapGold() - (used?.SellGoldUsed ?? 0);
                expectedGold = Math.Max(0, Math.Min(amount * sell, remainingAll));
                break;
            }
        }
        if (ctx.Lang == "es")
        {
            var es = new Dictionary<string, int>();
            foreach (var kv in costs) es[OrderTexts.CostKey(ctx.Lang, kv.Key)] = kv.Value;
            return es;
        }
        return costs;
    }

    private static int FortLadderIndexLocal(string? fort) => (fort ?? "").ToLower() switch
    {
        "tower" or "palisade" => 0,
        "fort" => 1,
        "stone walls" or "castle" => 2,
        "walls" or "keep" or "fortress" => 3,
        "citadel walls" or "citadel" => 4,
        _ => -1
    };

    private static int StockOf(Nation n, string p) => p switch
    {
        "timber" => n.Timber, "leather" => n.Leather, "bronze" => n.Bronze,
        "steel" => n.Steel, "mithril" => n.Mithril, "mounts" => n.Mounts, "food" => n.Food, _ => 0
    };

    private HexTile? TileAt(string gameId, string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r)) return null;
        return _db.HexTiles.FirstOrDefault(h => h.GameId == gameId && h.Q == q && h.R == r);
    }
    /// <summary>Full estimate pipeline for one order (form + errors + costs).</summary>
    public async Task<object> EstimateAsync(string gameId, EstimateOrderRequest req, string lang, string nationId)
    {
        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == req.CharacterId && c.NationId == nationId);
        if (ch == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.character-not-found"));

        var game = await _db.Games
            .AsSplitQuery()
            .Include(g => g.Nations).ThenInclude(n => n.Characters)
            .Include(g => g.Nations).ThenInclude(n => n.Armies)
            .Include(g => g.Nations).ThenInclude(n => n.Navies)
            .Include(g => g.Nations).ThenInclude(n => n.PopulationCentres)
            .Include(g => g.Nations).ThenInclude(n => n.Relations)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.game-not-found"));
        var nation = game.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.nation-not-found"));

        var def = OrderDefinitions.Orders.FirstOrDefault(o => o.Code == req.Code);
        if (def == null) throw new OrderRequestException(400, OrderTexts.Get(lang, "err.invalid-order-code"));

        var pars = ParseEstimateParams(req.Parameters);
        string? afterDest = null;
        if (req.AfterOrder != null && MoveCodes.Contains(req.AfterOrder.Code))
        {
            var ap = ParseEstimateParams(req.AfterOrder.Parameters);
            if (ap.TryGetValue("destination", out var dEl) && dEl.ValueKind == System.Text.Json.JsonValueKind.String)
                afterDest = dEl.GetString();
        }
        var effLoc = afterDest ?? ch.LocationHex;

        var commandsNavy = nation.Navies.Any(v => v.CommanderId == ch.Id);
        var (okElig, reason) = CheckEligible(ch, def, nation, commandsNavy, lang);
        if (!okElig)
            return new { ok = false, errors = new[] { reason }, costs = new { }, requires = Array.Empty<object>(),
                effectiveLocation = effLoc, movedByFirstOrder = afterDest != null };

        var army = ResolveEstimateArmy(req.ArmyId, ch, nation);
        var navy = ResolveEstimateNavy(req.NavyId, ch, nation);

        var ctx = new EstimateCtx(game, nation, ch, army, navy, effLoc, pars, lang);
        var requires = RequiresFor(req.Code, ctx);
        var errors = ValidateEstimateParams(req.Code, ctx, requires);
        var pending = await PendingNationOrders(gameId, nation.Id);
        var used = SummarizePendingUsage(pending, nation, game);
        var costs = EstimateCosts(req.Code, ctx, out var maxAmount, out var expectedGold, used);
        if (maxAmount == 0)
            errors.Add(OrderTexts.Get(lang, "err.insufficient-resources-or-capacity-to-execute-ma"));
        var warnings = new List<string>();
        CrossOrderConflicts(req.Code, ctx, pending, used, errors, warnings);

        object? suggestNames = null;
        if (req.Code is 552 or 555)
            suggestNames = TurnProcessor.CampSuggestions(nation.Name);

        return new
        {
            ok = errors.Count == 0,
            errors,
            warnings,
            costs,
            maxAmount,
            expectedGold,
            requires,
            effectiveLocation = effLoc,
            movedByFirstOrder = afterDest != null,
            suggestNames
        };
    }

    /// <summary>Eligibility of every order for one character.</summary>
    public async Task<object> EligibleAsync(string gameId, string characterId, string lang, string nationId)
    {
        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.NationId == nationId);
        if (ch == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.character-not-found"));

        var nation = await _db.Nations
            .Include(n => n.Armies).Include(n => n.Navies).Include(n => n.PopulationCentres)
            .FirstOrDefaultAsync(n => n.Id == nationId);
        var commandsNavy = nation?.Navies.Any(v => v.CommanderId == ch.Id) == true;

        var list = OrderDefinitions.Orders.Select(d =>
        {
            var (ok, reason) = CheckEligible(ch, d, nation, commandsNavy, lang);
            return new { code = d.Code, ok, reason };
        }).ToList();
        return new { eligible = list };
    }
}
