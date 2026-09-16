using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Orders;

/// <summary>Per-order form shapes: which fields each order asks for.</summary>
public sealed partial class OrderEstimateService
{
    private static List<OrderFieldSpecDto> TroopAmountFields(EstimateScope scope)
    {
        string L(string key) => OrderTexts.Get(scope.Lang, key);
        return new()
        {
            Num("hc", L("label.heavy-cavalry"), req: false, min: 0),
            Num("lc", L("label.light-cavalry"), req: false, min: 0),
            Num("hi", L("label.heavy-infantry"), req: false, min: 0),
            Num("li", L("label.light-infantry"), req: false, min: 0),
            Num("ar", L("opt.archers"), req: false, min: 0),
            Num("ma", L("label.men-at-arms"), req: false, min: 0),
        };
    }

    private static IEnumerable<Character> LiveCharacters(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead);

    private static IEnumerable<Character> ForeignCharacters(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != scope.Nation.Id && !c.IsDead);

    private static IEnumerable<Character> TransferableCharacters(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead && !c.IsKidnapped);

    private static IEnumerable<Character> CompanyCommanders(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters).Where(c => c.CompanyId != null && !c.IsDead);

    private static IEnumerable<Character> AvailableCommanders(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId == scope.Nation.Id && c.CommandSkill > 0 && !c.IsDead);

    private static IEnumerable<Character> ForeignEmissaries(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters).Where(c => c.EmissarySkill > 0 && !c.IsDead);

    private static IEnumerable<Character> Hostages(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters).Where(c => c.IsKidnapped && !c.IsDead);

    private static IEnumerable<Character> SkilledForeigners(EstimateScope scope) =>
        scope.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != scope.Nation.Id && (c.EmissarySkill > 0 || c.AgentSkill > 0) && !c.IsDead);

    private List<OrderFieldOptionDto> CapitalCandidates(EstimateScope scope) => scope.Nation.PopulationCentres
        .Where(p => p.Size == "major town" || p.Size == "city")
        .Select(p => new OrderFieldOptionDto(p.Id, p.Name)).ToList();

    private List<OrderFieldOptionDto> StealableArtifacts(EstimateScope scope)
    {
        var lang = scope.Lang;
        var character = scope.Character;
        return _db.Artifacts
            .Where(a => (a.HeldByCharacterId != null && a.HeldByCharacterId != character.Id)
                || (a.HeldByCharacterId == null && a.LocationHex == scope.EffectiveLocation))
            .ToList()
            .Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId == null
                ? $"{OrderTexts.ArtifactDisplayName(lang, a)} @ {(a.LocationHex ?? "?")}"
                : OrderTexts.ArtifactDisplayName(lang, a)))
            .ToList();
    }

    private List<OrderFieldOptionDto> LooseArtifacts(EstimateScope scope) =>
        _db.Artifacts.Where(a => a.HeldByCharacterId == null).ToList()
            .Select(a => new OrderFieldOptionDto(a.Id,
                $"{OrderTexts.ArtifactDisplayName(scope.Lang, a)} @ {a.LocationHex ?? "?"}"))
            .ToList();

    private List<OrderFieldSpecDto> LoreFields(EstimateScope scope)
    {
        var lang = scope.Lang;
        var fields = new List<OrderFieldSpecDto>
        {
            Sel("spellId", OrderTexts.Get(lang, "label.spell"), scope.KnownSpellOptions(SpellType.Lore))
        };
        var spellId = scope.Number("spellId", -1);
        if (LoreCharSpells.Contains(spellId))
            fields.Add(Sel("targetId", OrderTexts.Get(lang, "label.target-character"),
                scope.CharacterOptions(LiveCharacters(scope))));
        else if (LoreCommanderSpells.Contains(spellId))
            fields.Add(Sel("commanderId", OrderTexts.Get(lang, "label.force-commander"),
                scope.CharacterOptions(LiveCharacters(scope).Where(c =>
                    c.ArmyId != null || scope.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id)))));
        else if (LoreNationSpells.Contains(spellId))
            fields.Add(Sel("nationId", OrderTexts.Get(lang, "label.target-nation"), scope.AllNations()));
        else if (LoreAllegianceSpells.Contains(spellId))
            fields.Add(Sel("allegiance", OrderTexts.Get(lang, "label.allegiance"), AllegianceOptions(lang)));
        else if (LoreArtifactSpells.Contains(spellId))
            fields.Add(Sel("artifactId", OrderTexts.Get(lang, "label.artifact"), _db.Artifacts.ToList()
                .Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId != null
                    ? $"{OrderTexts.ArtifactDisplayName(lang, a)} {OrderTexts.Get(lang, "opt.held")}"
                    : $"{OrderTexts.ArtifactDisplayName(lang, a)} @ {a.LocationHex ?? "?"}"))
                .ToList()));
        else
            fields.Add(Hx("hex", OrderTexts.Get(lang, "label.hex-empty-here"), req: false));
        return fields;
    }

    private static OrderFieldSpecDto Num(string key, string label, bool req = true, int? min = null, int? max = null) =>
        new(key, label, "number", req, min, max);
    private static OrderFieldSpecDto Txt(string key, string label, bool req = true) =>
        new(key, label, "text", req);
    private static OrderFieldSpecDto Hx(string key, string label, bool req = true) =>
        new(key, label, "hex", req);
    private static OrderFieldSpecDto Sel(string key, string label, List<OrderFieldOptionDto> opts, bool req = true) =>
        new(key, label, "select", req, Options: opts);
    private static OrderFieldSpecDto Multi(string key, string label, List<OrderFieldOptionDto> opts) =>
        new(key, label, "multiselect", false, Options: opts);
    private static OrderFieldSpecDto Flag(string key, string label) =>
        new(key, label, "flag", false);

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
        TurnProcessor.MarketProductList().Select(p => new OrderFieldOptionDto(p, OrderTexts.ProductName(lang, p))).ToList();
    private static List<OrderFieldOptionDto> LevelOptions(string? lang) => new()
    {
        new("1", OrderTexts.Get(lang, "opt.1-tower")), new("2", OrderTexts.Get(lang, "opt.2-fort")),
        new("3", OrderTexts.Get(lang, "opt.3-castle")), new("4", OrderTexts.Get(lang, "opt.4-keep")),
        new("5", OrderTexts.Get(lang, "opt.5-citadel"))
    };
    // Nombres de naciones/personajes/hechizos/artefactos: ver OrderTexts.
    // Hechizos de lore por tipo de diana (wiki: Required Information).
    private static readonly HashSet<int> LoreCharSpells = new() { 408, 420, 422, 424, 430, 436 };
    private static readonly HashSet<int> LoreCommanderSpells = new() { 406, 417, 426 };
    private static readonly HashSet<int> LoreNationSpells = new() { 404, 419, 432 };
    private static readonly HashSet<int> LoreAllegianceSpells = new() { 402, 410 };
    private static readonly HashSet<int> LoreArtifactSpells = new() { 412, 418, 428 };

    public List<OrderFieldSpecDto> RequiresFor(int code, EstimateCtx ctx)
    {
        var scope = new EstimateScope(_db, ctx);
        var lang = scope.Lang;
        var ownCharacters = scope.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId == scope.Nation.Id && !c.IsDead).ToList();
        var foesAtLocation = scope.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != scope.Nation.Id && !c.IsDead && c.LocationHex == scope.EffectiveLocation).ToList();
        string L(string key) => OrderTexts.Get(lang, key);

        return code switch
        {
            175 => new() { Sel("allegiance", L("label.allegiance"), AllegianceOptions(lang)) },
            180 or 185 => new() { Sel("nationId", L("label.nation"), scope.AllNations()) },
            690 => new() { Sel("nationId", L("label.victim-nation"), scope.AllNations()), Num("amount", L("label.gold-amount"), min: 1) },
            210 or 615 or 620 => new() { Sel("targetId", L("label.target-character"), scope.CharacterOptions(foesAtLocation)) },
            225 or 330 => new() { Sel("spellId", L("label.spell"),
                code == 225 ? scope.KnownSpellOptions(SpellType.Combat) : scope.KnownSpellOptions(SpellType.Conjuring)) },
            120 => new() { Sel("spellId", L("label.spell"), scope.KnownSpellOptions(SpellType.Heal)),
                Sel("targetId", L("label.target-character-same-hex-empty-self"), scope.CharacterOptions(scope.Game.Nations
                    .SelectMany(n => n.Characters).Where(c => !c.IsDead && c.LocationHex == scope.EffectiveLocation)), req: false) },
            705 => new() { Sel("spellId", L("label.spell-empty-random-research"), scope.ResearchableSpellOptions(), req: false) },
            230 or 235 or 240 or 250 or 255 or 260 => new() { Sel("tactic", L("label.tactic"), TacticOptions, req: false) },
            270 or 340 or 345 or 347 or 440 or 452 or 456
                => new() { Num("amount", L("label.amount"), min: 1) },
            275 or 280 => new() { Num("warships", L("label.warships-empty-all"), req: false, min: 0), Num("transports", L("label.transports-empty-all"), req: false, min: 0) },
            300 => new() { Num("newRate", L("label.new-tax-rate"), min: 10, max: 80) },
            347 or 349 or 351 or 353 or 355 => new() { Sel("destArmyId", L("label.destination-army"), scope.ArmyOptions()), Num("amount", L("label.amount"), min: 1) },
            370 or 375 => new List<OrderFieldSpecDto>(TroopAmountFields(scope).Prepend(
                Sel("material", L("label.material"), MaterialOptions(lang)))),
            400 or 404 or 408 or 412 => new() { Num("amount", L("label.troops"), min: 1),
                Sel("weapons", L("label.weapon-material-bronze-steel-mithril"), MaterialOptions(lang).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()), Sel("armour", L("label.armour-material"), MaterialOptions(lang)) },
            416 or 420 => new() { Num("amount", L("label.troops"), min: 1) },
            425 => TroopAmountFields(scope),
            444 => new() { Num("amount", L("label.rank-points"), min: 1), Sel("material", L("label.material"), MaterialOptions(lang).Where(m => m.Value != "wood").ToList()) },
            448 => new() { Num("amount", L("label.rank-points"), min: 1), Sel("material", L("label.material"), MaterialOptions(lang).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()) },
            494 => new() { Sel("level", L("label.fort-level-empty-next"), LevelOptions(lang), req: false) },
            470 or 498 => new() { Num("amount", L("label.amount"), req: false, min: 0) },
            725 or 728 or 731 or 734 or 737 => new() { Txt("name", L("label.name-5-17-letters-capitalized")),
                Num("command", L("label.command-0-30"), req: false, min: 0, max: 30), Num("agent", L("label.agent-0-30"), req: false, min: 0, max: 30),
                Num("emissary", L("label.emissary-0-30"), req: false, min: 0, max: 30), Num("mage", L("label.mage-0-30"), req: false, min: 0, max: 30) },
            745 => new() { Txt("name", L("label.company-name")) },
            770 => new() { Txt("name", L("label.army-name")), Num("troops", L("label.troops"), min: 1),
                Sel("troopType", L("label.troop-type"), TroopTypeOptions(lang)), Sel("weapons", L("label.weapons"), MaterialOptions(lang).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()),
                Sel("armour", L("label.armour"), MaterialOptions(lang)), Num("food", L("label.food-units"), req: false, min: 0) },
            755 => new() { Sel("commanderId", L("label.company-commander"), scope.CharacterOptions(CompanyCommanders(scope))) },
            765 => new() { Sel("commanderId", L("label.new-commander"), scope.CharacterOptions(AvailableCommanders(scope))) },
            785 => new() { Sel("commanderId", L("label.force-commander"), scope.CharacterOptions(LiveCharacters(scope))) },
            780 => new() { Sel("targetId", L("label.new-commander"), scope.CharacterOptions(ownCharacters)) },
            910 or 915 or 920 or 930 or 475 or 490 or 605 or 665 or 670 or 675 or 680 => new() { Hx("hex", L("label.hex-empty-current-location"), req: false) },
            610 or 625 or 630 or 635 or 640 or 645 or 650 or 655 or 363 => new()
                { Sel("targetId", L("label.target"), scope.CharacterOptions(LiveCharacters(scope))) },
            685 => new() { Sel("artifactId", L("label.artifact"), StealableArtifacts(scope)) },
            505 => new() { Sel("targetId", L("label.target-character"), scope.CharacterOptions(ForeignCharacters(scope))), Num("amount", L("label.bribe-gold-min-500"), req: false, min: 500) },
            552 or 555 => new() { Txt("name", L("label.camp-name-empty-nation-pool"), req: false) },
            560 or 565 or 580 or 585 => new() { Hx("hex", L("label.hex-empty-current-location"), req: false) },
            360 => new() { Multi("artifactId", L("label.artifacts"), scope.HeldArtifacts()), Sel("targetId", L("label.to-character-same-hex"), scope.CharacterOptions(TransferableCharacters(scope))) },
            792 or 796 => new() { Multi("artifactId", L("label.artifacts-1-6"), scope.HeldArtifacts()) },
            700 => new() { Multi("spellId", L("label.spells-to-forget-1-6"), scope.KnownSpellOptionsForForget()) },
            798 => new() { Num("amount", L("label.transports-to-pick-up"), min: 1) },
            205 or 945 => new() { Sel("artifactId", L("label.artifact"), scope.HeldArtifacts()) },
            805 => new() { Sel("artifactId", L("label.movement-artifact"), scope.HeldArtifacts()), Hx("destination", L("label.destination-hex-empty-stay"), req: false) },
            935 => new() { Sel("artifactId", L("label.artifact"), scope.HeldArtifacts()), Hx("hex", L("label.hex-to-scry-empty-here"), req: false) },
            900 => new() { Sel("artifactId", L("label.artifact-optional"), LooseArtifacts(scope), req: false) },
            905 => new() { Sel("commanderId", L("label.force-commander"), scope.CharacterOptions(LiveCharacters(scope))), Flag("follow", L("label.follow")), Hx("hex", L("label.hex-empty-force-location"), req: false) },
            940 => LoreFields(scope),
            949 => new() { Sel("targetId", L("label.receiving-emissary-other-nation-same-hex"), scope.CharacterOptions(ForeignEmissaries(scope))) },
            950 => new() { Sel("pcId", L("label.new-capital-major-town-city"), CapitalCandidates(scope)) },
            660 => new() { Sel("targetId", L("label.hostage"), scope.CharacterOptions(Hostages(scope))), Num("amount", L("label.ransom-gold-empty-1000"), req: false, min: 1) },
            500 => new() { Sel("targetId", L("label.target-character-emissary-agent-same-hex"), scope.CharacterOptions(SkilledForeigners(scope))) },
            810 or 820 or 830 or 850 or 860 or 870 => new() { Txt("destination", L("label.destination-hex")), Flag("evasive", L("label.evasive")) },
            825 => new() { Sel("spellId", L("label.spell"), scope.KnownSpellOptions(SpellType.Movement)), Txt("destination", L("label.destination-hex")) },
            310 => new() { Sel("product", L("label.product"), ProductOptions(lang)), Num("amount", L("label.amount"), min: 1), Num("price", L("label.bid-price-empty-market"), req: false, min: 1) },
            315 or 320 => new() { Sel("product", L("label.product"), ProductOptions(lang)), Num("amount", L("label.amount"), min: 1) },
            325 => new() { Sel("product", L("label.product"), ProductOptions(lang)), Num("percentage", L("label.percentage-of-stock"), min: 1, max: 100) },
            947 or 948 => new() { Sel("resource", L("label.resource"), ProductOptions(lang)), Num("amount", L("label.amount"), min: 1) },
            _ => new List<OrderFieldSpecDto>()
        };
    }
}
