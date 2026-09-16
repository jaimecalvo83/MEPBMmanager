namespace MEPBMmanager.Domain.Constants;

/// <summary>
/// Habilidades especiales del módulo Third Age 2950, una por nación según su
/// página de la wiki (sección "Special Abilities", p. ej. /2950/woodmen).
/// Solo NOMBRA: cada ID describe una regla del módulo; la columna Hook indica
/// dónde debe aplicarse en el backend (hoy ninguna está aplicada).
/// Claves de nación = slugs de Map2950Seeder (woodmen, northmen, ...).
/// </summary>
public static class NationAbilities
{
    // ── Marcha forzada / moral ──────────────────────────────────────────
    // FORCE_MARCH_NONE: marchar sin perder moral. Hook: ProcessForceMarch.
    // FORCE_MARCH_FED_12: 1-2 con comida, 2-5 sin comida. Hook: idem.
    // FORCE_MARCH_HARDY: 1-2 forzando con comida; sin comida +1-2 quieto,
    //   -1-2 moviendo, -2-5 forzando. Hook: idem + MovementResolver (unfed).
    // ── Reclutas (training base al reclutar) ────────────────────────────
    // RECRUIT_TRAINING_20/25, _HI_30 (solo HI), _MA_25 (solo MA). Hook: reclutas.
    // ── Nombrar personajes (tope 40 con la orden) ───────────────────────
    // NAME_COMMANDER_40 (728), NAME_AGENT_40 (731), NAME_EMISSARY_40 (734),
    // NAME_MAGE_40 (737). Hook: ProcessNameCharacter.
    // ── Mercado / barcos / fuertes / contratación ──────────────────────
    // MARKET_BUY_20 (-20% comprar), MARKET_SELL_20 (+20% vender). Hook: mercado.
    // SHIP_TIMBER_750/500 (madera por barco). Hook: ProcessMakeWarships/Transports.
    // FORT_HALF_TIMBER (50% madera). Hook: ProcessFortifyPC (hoy no cobra).
    // HIRE_FREE (contratar gratis), START_MORALE_40. Hook: ProcessHireArmy.
    // ── Rangos efectivos ────────────────────────────────────────────────
    // SCOUT_DOUBLE (905,910,915,920,925,930 al doble), SCOUT_AS_50 (idem a 50),
    // UNCOVER_AS_40 (585 a 40), AGENT_PLUS_20 (615,620 +20). Hook: esas órdenes
    // (hoy ni tiran habilidad).
    // ── Personajes nuevos ───────────────────────────────────────────────
    // NEWCHAR_STEALTH / NEWCHAR_CHALLENGE (probabilidad/bonus). Hook: nombrar.
    // ── Magia ───────────────────────────────────────────────────────────
    // LOST_SPELL_<id> (hechizo perdido aprendible). Hook: ProcessPrenticeMagery
    // (hoy da al azar de todo el catálogo).
    // NO_STORMS (inmune a tormentas/perderse). Hook: inexistente (no hay tormentas).

    /// <summary>Nación (slug) → IDs de habilidad.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ByNationSlug =
        new Dictionary<string, string[]>
        {
            ["woodmen"] = ["NEWCHAR_STEALTH", "NEWCHAR_CHALLENGE", "SCOUT_DOUBLE", "FORCE_MARCH_HARDY"],
            ["northmen"] = ["NAME_EMISSARY_40", "MARKET_BUY_20", "MARKET_SELL_20", "RECRUIT_TRAINING_20", "SHIP_TIMBER_750"],
            ["riders-of-rohan"] = ["RECRUIT_TRAINING_20", "NAME_COMMANDER_40", "FORCE_MARCH_NONE", "LOST_SPELL_508"],
            ["dunadan-rangers"] = ["RECRUIT_TRAINING_20", "FORCE_MARCH_FED_12", "FORT_HALF_TIMBER", "NAME_MAGE_40"],
            ["silvan-elves"] = ["SHIP_TIMBER_500", "RECRUIT_TRAINING_25", "NEWCHAR_STEALTH", "FORCE_MARCH_FED_12"],
            ["northern-gondor"] = ["NAME_COMMANDER_40", "FORCE_MARCH_FED_12", "FORT_HALF_TIMBER", "RECRUIT_TRAINING_20"],
            ["southern-gondor"] = ["RECRUIT_TRAINING_20", "FORCE_MARCH_FED_12", "FORT_HALF_TIMBER", "NAME_MAGE_40"],
            ["dwarves"] = ["RECRUIT_TRAINING_HI_30", "FORCE_MARCH_NONE", "FORT_HALF_TIMBER", "SCOUT_AS_50"],
            ["sinda-elves"] = ["SHIP_TIMBER_500", "RECRUIT_TRAINING_25", "NEWCHAR_STEALTH", "FORCE_MARCH_FED_12"],
            ["noldo-elves"] = ["RECRUIT_TRAINING_25", "FORCE_MARCH_NONE", "UNCOVER_AS_40", "NEWCHAR_STEALTH"],
            ["witch-king"] = ["NAME_COMMANDER_40", "FORCE_MARCH_HARDY", "LOST_SPELL_244", "LOST_SPELL_512"],
            ["dragon-lord"] = ["NEWCHAR_STEALTH", "LOST_SPELL_314", "SCOUT_DOUBLE", "FORCE_MARCH_HARDY"],
            ["dog-lord"] = ["RECRUIT_TRAINING_20", "FORCE_MARCH_HARDY", "LOST_SPELL_508", "NEWCHAR_STEALTH"],
            ["cloud-lord"] = ["AGENT_PLUS_20", "NEWCHAR_STEALTH", "UNCOVER_AS_40", "NAME_AGENT_40"],
            ["blind-sorcerer"] = ["NO_STORMS", "NAME_MAGE_40", "LOST_SPELL_246", "LOST_SPELL_512"],
            ["ice-king"] = ["NEWCHAR_STEALTH", "NAME_AGENT_40", "LOST_SPELL_246", "FORCE_MARCH_HARDY"],
            ["quiet-avenger"] = ["SCOUT_DOUBLE", "UNCOVER_AS_40", "NAME_EMISSARY_40", "NAME_COMMANDER_40"],
            ["fire-king"] = ["HIRE_FREE", "START_MORALE_40", "FORCE_MARCH_HARDY", "LOST_SPELL_248", "LOST_SPELL_512"],
            ["long-rider"] = ["NAME_COMMANDER_40", "LOST_SPELL_508", "RECRUIT_TRAINING_20", "FORCE_MARCH_NONE"],
            ["dark-lieutenants"] = ["LOST_SPELL_244", "LOST_SPELL_512", "FORCE_MARCH_HARDY", "NAME_COMMANDER_40"],
            ["corsairs"] = ["NO_STORMS", "SHIP_TIMBER_750", "NEWCHAR_CHALLENGE"],
            ["rhun-easterlings"] = ["NAME_COMMANDER_40", "FORCE_MARCH_HARDY", "NEWCHAR_CHALLENGE"],
            ["dunlendings"] = ["NEWCHAR_CHALLENGE", "SCOUT_DOUBLE", "NAME_AGENT_40"],
            ["white-wizard"] = ["HIRE_FREE", "RECRUIT_TRAINING_MA_25", "NAME_COMMANDER_40", "NEWCHAR_CHALLENGE"],
            ["khand-easterlings"] = ["NAME_COMMANDER_40", "NEWCHAR_CHALLENGE", "FORCE_MARCH_HARDY"],
        };

    /// <summary>ID → nombre visible (EN).</summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayNamesEn =
        new Dictionary<string, string>
        {
            ["FORCE_MARCH_NONE"] = "Force march with no morale loss",
            ["FORCE_MARCH_FED_12"] = "Force march: 1-2 fed, 2-5 unfed",
            ["FORCE_MARCH_HARDY"] = "Hardy march morale",
            ["RECRUIT_TRAINING_20"] = "Recruits with training 20",
            ["RECRUIT_TRAINING_25"] = "Recruits with training 25",
            ["RECRUIT_TRAINING_HI_30"] = "Heavy infantry recruited with training 30",
            ["RECRUIT_TRAINING_MA_25"] = "Men-at-arms recruited with training 25",
            ["NAME_COMMANDER_40"] = "Nameable commanders up to 40 (728)",
            ["NAME_AGENT_40"] = "Nameable agents up to 40 (731)",
            ["NAME_EMISSARY_40"] = "Nameable emissaries up to 40 (734)",
            ["NAME_MAGE_40"] = "Nameable mages up to 40 (737)",
            ["MARKET_BUY_20"] = "20% cheaper market buys",
            ["MARKET_SELL_20"] = "20% dearer market sells",
            ["SHIP_TIMBER_750"] = "Ships for 750 timber",
            ["SHIP_TIMBER_500"] = "Ships for 500 timber",
            ["FORT_HALF_TIMBER"] = "Fortifications at half timber",
            ["SCOUT_DOUBLE"] = "Scout at double range (905-930)",
            ["SCOUT_AS_50"] = "Scout as rank 50",
            ["UNCOVER_AS_40"] = "Uncover secrets as rank 40 (585)",
            ["AGENT_PLUS_20"] = "+20 agent on assassinate/kidnap (615/620)",
            ["NEWCHAR_STEALTH"] = "Extra stealth on new characters",
            ["NEWCHAR_CHALLENGE"] = "Extra challenge on new characters",
            ["LOST_SPELL_508"] = "Learnable: Conjure Mounts (508)",
            ["LOST_SPELL_244"] = "Learnable: Fearful Hearts (244)",
            ["LOST_SPELL_512"] = "Learnable: Conjure Hordes (512)",
            ["LOST_SPELL_314"] = "Learnable: Teleport (314)",
            ["LOST_SPELL_246"] = "Learnable: Summon Storms (246)",
            ["LOST_SPELL_248"] = "Learnable: Fanaticism (248)",
            ["NO_STORMS"] = "Immune to storms and getting lost at sea",
            ["HIRE_FREE"] = "Free army hire",
            ["START_MORALE_40"] = "New armies with morale 40",
        };

    /// <summary>ID → nombre visible.</summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>
        {
            ["FORCE_MARCH_NONE"] = "Marcha forzada sin pérdida de moral",
            ["FORCE_MARCH_FED_12"] = "Marcha forzada: 1-2 con comida, 2-5 sin ella",
            ["FORCE_MARCH_HARDY"] = "Moral de marcha recia",
            ["RECRUIT_TRAINING_20"] = "Reclutas con training 20",
            ["RECRUIT_TRAINING_25"] = "Reclutas con training 25",
            ["RECRUIT_TRAINING_HI_30"] = "Infantería pesada reclutada con training 30",
            ["RECRUIT_TRAINING_MA_25"] = "Men-at-arms reclutados con training 25",
            ["NAME_COMMANDER_40"] = "Comandantes nombrables hasta 40 (728)",
            ["NAME_AGENT_40"] = "Agentes nombrables hasta 40 (731)",
            ["NAME_EMISSARY_40"] = "Emisarios nombrables hasta 40 (734)",
            ["NAME_MAGE_40"] = "Magos nombrables hasta 40 (737)",
            ["MARKET_BUY_20"] = "Compra en mercado un 20% más barato",
            ["MARKET_SELL_20"] = "Vende en mercado un 20% más caro",
            ["SHIP_TIMBER_750"] = "Barcos por 750 de madera",
            ["SHIP_TIMBER_500"] = "Barcos por 500 de madera",
            ["FORT_HALF_TIMBER"] = "Fortificaciones a mitad de madera",
            ["SCOUT_DOUBLE"] = "Explorar al doble de rango (905-930)",
            ["SCOUT_AS_50"] = "Explorar como rango 50",
            ["UNCOVER_AS_40"] = "Destapar secretos como rango 40 (585)",
            ["AGENT_PLUS_20"] = "+20 de agente en asesinar/secuestrar (615/620)",
            ["NEWCHAR_STEALTH"] = "Sigilo extra en personajes nuevos",
            ["NEWCHAR_CHALLENGE"] = "Desafío extra en personajes nuevos",
            ["LOST_SPELL_508"] = "Aprendible: Conjure Mounts (508)",
            ["LOST_SPELL_244"] = "Aprendible: Fearful Hearts (244)",
            ["LOST_SPELL_512"] = "Aprendible: Conjure Hordes (512)",
            ["LOST_SPELL_314"] = "Aprendible: Teleport (314)",
            ["LOST_SPELL_246"] = "Aprendible: Summon Storms (246)",
            ["LOST_SPELL_248"] = "Aprendible: Fanaticism (248)",
            ["NO_STORMS"] = "Inmunes a tormentas y perderse en el mar",
            ["HIRE_FREE"] = "Contratar ejércitos gratis",
            ["START_MORALE_40"] = "Ejércitos nuevos con moral 40",
        };

    public static bool Has(string nationSlug, string abilityId) =>
        ByNationSlug.TryGetValue(nationSlug, out var list) && list.Contains(abilityId);

    /// <summary>DisplayName del módulo ("Dúnadan Rangers", ...) → slug.</summary>
    public static readonly IReadOnlyDictionary<string, string> SlugByDisplayName =
        new Dictionary<string, string>
        {
            ["Woodmen"] = "woodmen",
            ["Northmen"] = "northmen",
            ["Riders of Rohan"] = "riders-of-rohan",
            ["Dúnadan Rangers"] = "dunadan-rangers",
            ["Silvan Elves"] = "silvan-elves",
            ["Northern Gondor"] = "northern-gondor",
            ["Southern Gondor"] = "southern-gondor",
            ["Dwarves"] = "dwarves",
            ["Sinda Elves"] = "sinda-elves",
            ["Noldo Elves"] = "noldo-elves",
            ["Witch-king"] = "witch-king",
            ["Dragon Lord"] = "dragon-lord",
            ["Dog Lord"] = "dog-lord",
            ["Cloud Lord"] = "cloud-lord",
            ["Blind Sorcerer"] = "blind-sorcerer",
            ["Ice King"] = "ice-king",
            ["Quiet Avenger"] = "quiet-avenger",
            ["Fire King"] = "fire-king",
            ["Long Rider"] = "long-rider",
            ["Dark Lieutenants"] = "dark-lieutenants",
            ["Corsairs"] = "corsairs",
            ["Dunlendings"] = "dunlendings",
            ["Khand Easterlings"] = "khand-easterlings",
            ["Rhûn Easterlings"] = "rhun-easterlings",
            ["White Wizard"] = "white-wizard",
        };

    public static bool HasForNation(string? nationNameOrSlug, string abilityId)
    {
        if (string.IsNullOrEmpty(nationNameOrSlug)) return false;
        if (ByNationSlug.ContainsKey(nationNameOrSlug))
            return Has(nationNameOrSlug, abilityId);
        return SlugByDisplayName.TryGetValue(nationNameOrSlug, out var slug) && Has(slug, abilityId);
    }

    /// <summary>Training base de reclutas por nación y tropa (10 por defecto).</summary>
    public static int RecruitTrainingFor(string? nationNameOrSlug, string troopType) =>
        HasForNation(nationNameOrSlug, "RECRUIT_TRAINING_HI_30") && troopType == "HeavyInfantry" ? 30
        : HasForNation(nationNameOrSlug, "RECRUIT_TRAINING_MA_25") && troopType == "MenAtArms" ? 25
        : HasForNation(nationNameOrSlug, "RECRUIT_TRAINING_25") ? 25
        : HasForNation(nationNameOrSlug, "RECRUIT_TRAINING_20") ? 20
        : 10;

    /// <summary>Rango inicial al nombrar pj del tipo (40 con NAME_*_40, 15 base).</summary>
    public static int NameCharacterSkill(string? nationNameOrSlug, string type) =>
        type == "commander" && HasForNation(nationNameOrSlug, "NAME_COMMANDER_40") ? 40
        : type == "agent" && HasForNation(nationNameOrSlug, "NAME_AGENT_40") ? 40
        : type == "emissary" && HasForNation(nationNameOrSlug, "NAME_EMISSARY_40") ? 40
        : type == "mage" && HasForNation(nationNameOrSlug, "NAME_MAGE_40") ? 40
        : 15;

    private static readonly HashSet<int> ScoutDoubleCodes = [905, 910, 915, 920, 925, 930];

    /// <summary>
    /// Rango efectivo para explorar/destapar (925 usa mando, resto agente):
    /// SCOUT_DOUBLE = doble; SCOUT_AS_50 = 50 si no tiene rango.
    /// </summary>
    public static int ScoutSkill(string? nationNameOrSlug, int orderCode, int agentRank, int commandRank)
    {
        int rank = orderCode == 925 ? commandRank : agentRank;
        if (HasForNation(nationNameOrSlug, "SCOUT_DOUBLE") && ScoutDoubleCodes.Contains(orderCode))
            return rank * 2;
        if (HasForNation(nationNameOrSlug, "SCOUT_AS_50"))
            return rank == 0 ? 50 : rank;
        return rank;
    }

    /// <summary>Rango efectivo para destapar secretos (585): 40 mínimo con UNCOVER_AS_40.</summary>
    public static int UncoverSkill(string? nationNameOrSlug, int emissaryRank) =>
        HasForNation(nationNameOrSlug, "UNCOVER_AS_40") ? Math.Max(emissaryRank, 40) : emissaryRank;

    /// <summary>Rango efectivo para asesinar/secuestrar (615/620): +20 con AGENT_PLUS_20.</summary>
    public static int AssassinSkill(string? nationNameOrSlug, int agentRank) =>
        HasForNation(nationNameOrSlug, "AGENT_PLUS_20") ? agentRank + 20 : agentRank;

    /// <summary>Hechizos perdidos del catálogo (solo investigables con acceso).</summary>
    public static readonly HashSet<int> LostSpellIds = [244, 246, 248, 314, 508, 512];

    public static bool CanLearnLostSpell(string? nationNameOrSlug, int spellId) =>
        LostSpellIds.Contains(spellId) && HasForNation(nationNameOrSlug, $"LOST_SPELL_{spellId}");

    /// <summary>Precio de compra en mercado (MARKET_BUY_20 = -20%).</summary>
    public static int MarketBuyPrice(string? nationNameOrSlug, int basePrice) =>
        HasForNation(nationNameOrSlug, "MARKET_BUY_20") ? basePrice * 4 / 5 : basePrice;

    /// <summary>Precio de venta en mercado (MARKET_SELL_20 = +20%).</summary>
    public static int MarketSellPrice(string? nationNameOrSlug, int basePrice) =>
        HasForNation(nationNameOrSlug, "MARKET_SELL_20") ? basePrice * 6 / 5 : basePrice;

    /// <summary>Moral al contratar ejército (40 con START_MORALE_40, 30 base).</summary>
    public static int HireMorale(string? nationNameOrSlug) =>
        HasForNation(nationNameOrSlug, "START_MORALE_40") ? 40 : 30;

    /// <summary>Madera por barco (base 1500; 750/500 con descuento).</summary>
    public static int ShipTimberCost(string? nationNameOrSlug, int baseTimber = 1500) =>
        HasForNation(nationNameOrSlug, "SHIP_TIMBER_750") ? baseTimber / 2
        : HasForNation(nationNameOrSlug, "SHIP_TIMBER_500") ? baseTimber / 3
        : baseTimber;

    // Tabla D-2 del reglamento (oro/madera por tipo; Tower-Fort-Castle-Keep-Citadel).
    // Los niveles de la 494 se equiparan: palisade=Tower, walls=Fort,
    // castle=Castle, fortress=Keep. Madera de stores del PC, oro de la nación.
    private static readonly IReadOnlyDictionary<string, (int Gold, int Timber)> FortBaseCosts =
        new Dictionary<string, (int Gold, int Timber)>
        {
            ["palisade"] = (1000, 1000),
            ["walls"] = (3000, 3000),
            ["castle"] = (5000, 5000),
            ["fortress"] = (8000, 8000),
            ["tower"] = (1000, 1000),
            ["fort"] = (3000, 3000),
            ["keep"] = (8000, 8000),
            ["citadel"] = (12000, 12000),
        };

    private static (int Gold, int Timber) FortBase(string fortLevel) =>
        FortBaseCosts.TryGetValue((fortLevel ?? "").ToLower(), out var c) ? c : (0, 0);

    /// <summary>Madera para fortificar (mitad con FORT_HALF_TIMBER).</summary>
    public static int FortTimberCost(string? nationNameOrSlug, string fortLevel)
    {
        int b = FortBase(fortLevel).Timber;
        return HasForNation(nationNameOrSlug, "FORT_HALF_TIMBER") ? b / 2 : b;
    }

    /// <summary>Oro para fortificar.</summary>
    public static int FortGoldCost(string fortLevel) => FortBase(fortLevel).Gold;

    /// <summary>Oro por contratar ejército (5000; 0 con HIRE_FREE).</summary>
    public static int HireArmyCost(string? nationNameOrSlug) =>
        HasForNation(nationNameOrSlug, "HIRE_FREE") ? 0 : 5000;
}
