using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

public sealed partial class TurnProcessor
{
    private readonly MepbmDbContext _db;

    private readonly CombatResolver _combat;

    private readonly MovementResolver _movement = new();

    private readonly Random _rng = new();


    // Hechizos de combate (orden 225) pendientes este turno, por fuerza.
    private Dictionary<string, List<int>> _pendingArmySpells = new();

    private Dictionary<string, List<int>> _pendingNavySpells = new();


    public TurnProcessor(MepbmDbContext db, CombatResolver combat)
    {
        _db = db;
        _combat = combat;
    }


    public async Task<object?> ProcessTurnAsync(string gameId)
    {
        var game = await _db.Games
            .Include(g => g.Turns).ThenInclude(t => t.Orders).ThenInclude(o => o.Character)
            .Include(g => g.Turns).ThenInclude(t => t.Orders).ThenInclude(o => o.Nation)
            .Include(g => g.Turns).ThenInclude(t => t.Orders).ThenInclude(o => o.Navy)
            .Include(g => g.Turns).ThenInclude(t => t.Orders).ThenInclude(o => o.Army)
            .Include(g => g.Nations).ThenInclude(n => n.Armies).ThenInclude(a => a.Characters)
            .Include(g => g.Nations).ThenInclude(n => n.Navies)
            .Include(g => g.Nations).ThenInclude(n => n.PopulationCentres)
            .Include(g => g.Nations).ThenInclude(n => n.Relations)
            .Include(g => g.Nations).ThenInclude(n => n.Players)
            .Include(g => g.Nations).ThenInclude(n => n.Characters).ThenInclude(c => c.Spells)
            .Include(g => g.Nations).ThenInclude(n => n.Characters).ThenInclude(c => c.Artifacts)
            .Include(g => g.Nations).ThenInclude(n => n.Characters).ThenInclude(c => c.GuardedBy)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return null;

        var currentTurn = game.Turns.FirstOrDefault(t => t.Status == "orders_open");
        if (currentTurn == null)
            return new { error = "No open turn to process" };

        currentTurn.Status = "processing";

        var results = new List<object>();
        var seasonBonus = GetSeasonBonus(game.Turns.Count > 0 ? game.Turns.Count : 1);

        // â”€â”€ FASE ECONÃ“MICA â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var econResults = ProcessEconomy(game, seasonBonus);
        results.AddRange(econResults);

        // ── AUTO-ÓRDENES PARA NACIONES INACTIVAS ──
        var nationsWithOrders = currentTurn.Orders.Select(o => o.NationId).Distinct().ToHashSet();
        foreach (var nation in game.Nations)
        {
            if (nationsWithOrders.Contains(nation.Id)) continue;
            foreach (var ch in nation.Characters.Where(c => !c.IsDead && !c.IsKidnapped))
            {
                _db.Orders.Add(new Order
                {
                    Id = Guid.NewGuid().ToString(),
                    GameId = game.Id,
                    TurnId = currentTurn.Id,
                    NationId = nation.Id,
                    CharacterId = ch.Id,
                    Code = 100,
                    Parameters = "{}",
                    Status = "auto_hold"
                });
            }
        }
        await _db.SaveChangesAsync();

        // â”€â”€ FASE DE DESAFÃOS PERSONALES (orden 210, reglamento G-1) â”€â”€
        ResolvePersonalChallenges(game, currentTurn, results);

        // â”€â”€ FASE DE Ã“RDENES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Recopilar hechizos de combate (225) para aplicarlos en las batallas.
        _pendingArmySpells = new();
        _pendingNavySpells = new();
        _recruitsUsed.Clear();
        _fortifiedThisTurn.Clear();
        _nationSellValue.Clear();
        foreach (var pr in MarketProducts) _marketBuyLeft[pr] = MarketBuyPool[pr];
        foreach (var o in currentTurn.Orders.Where(o => o.Code == 225 && o.Status == "pending"))
        {
            var sp = SpellFromParameters(o.Parameters);
            if (sp == null) continue;
            if (!string.IsNullOrEmpty(o.ArmyId))
            {
                if (!_pendingArmySpells.ContainsKey(o.ArmyId)) _pendingArmySpells[o.ArmyId] = new();
                _pendingArmySpells[o.ArmyId].Add(sp.Value);
            }
            else if (!string.IsNullOrEmpty(o.NavyId))
            {
                if (!_pendingNavySpells.ContainsKey(o.NavyId)) _pendingNavySpells[o.NavyId] = new();
                _pendingNavySpells[o.NavyId].Add(sp.Value);
            }
        }

        var orders = currentTurn.Orders.Where(o => o.Status == "pending").ToList();
        foreach (var order in orders)
        {
            var orderResult = ProcessOrder(order, game);
            results.Add(orderResult);
        }

        // â”€â”€ FASE DE AVANCE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        currentTurn.Status = "closed";
        currentTurn.ProcessedAt = DateTime.UtcNow;

        game.CurrentTurn = currentTurn.Number + 1;
        game.Status = "active";

        var nextDeadline = DateTime.UtcNow.AddDays(game.TurnIntervalDays);
        var nextTurn = new Turn
        {
            Id = Guid.NewGuid().ToString(),
            GameId = game.Id,
            Number = currentTurn.Number + 1,
            Status = "orders_open",
            Season = GetCurrentSeason(currentTurn.Number + 1),
            Deadline = nextDeadline
        };
        _db.Turns.Add(nextTurn);

        // Guardar resultado del turno
        var turnResult = new TurnResult
        {
            Id = Guid.NewGuid().ToString(),
            TurnId = currentTurn.Id,
            Content = JsonSerializer.Serialize(new { results })
        };
        _db.TurnResults.Add(turnResult);

        await _db.SaveChangesAsync();

        return new
        {
            message = $"Turn {currentTurn.Number} processed",
            turnNumber = currentTurn.Number,
            nextTurn = new { nextTurn.Id, nextTurn.Number, nextTurn.Status, nextTurn.Deadline },
            resultsCount = results.Count,
            results = results.Take(20).ToList()
        };
    }


    /// <summary>Impuesto oficial por PC: tamaño×tasa (camp 0).</summary>
    public static int GetPCTax(string size, int taxRate) => size.ToLower() switch
    {
        "city" => 100 * taxRate,
        "major town" => 75 * taxRate,
        "town" => 50 * taxRate,
        "village" => 25 * taxRate,
        _ => 0
    };


    /// <summary>Nivel de fuerte 1-5 por nombre (tipos oficiales).</summary>
    public static int FortLevel(string? fort)
    {
        var f = (fort ?? "").ToLower();
        if (f.Contains("citadel")) return 5;
        if (f == "castle" || f == "stone walls") return 3;
        if (f == "keep" || f == "walls" || f == "fortress") return 4;
        if (f.Contains("fort")) return 2;
        if (f.Contains("tower") || f.Contains("palisade")) return 1;
        return 0;
    }


    /// <summary>Índice 0-4 en la escalera Tower-Fort-Castle-Keep-Citadel (-1 sin nada).</summary>
    public static int FortLadderIndex(string? fort) => (fort ?? "").ToLower() switch
    {
        "tower" or "palisade" => 0,
        "fort" => 1,
        "stone walls" or "castle" => 2,
        "walls" or "keep" or "fortress" => 3,
        "citadel walls" or "citadel" => 4,
        _ => -1
    };


    /// <summary>Mantenimiento oficial en oro: tropas, puertos, barcos, fuertes y pjs.</summary>
    public static int NationUpkeep(Nation nation)
    {
        int upkeep = 0;
        foreach (var a in nation.Armies)
            upkeep += a.HeavyCavalry * 6 + a.LightCavalry * 3 + a.HeavyInfantry * 4
                + a.LightInfantry * 2 + a.Archers * 2 + a.MenAtArms;
        foreach (var pc in nation.PopulationCentres)
        {
            if (pc.HasHarbour) upkeep += 250;
            if (pc.HasPort) upkeep += 500;
            upkeep += FortLevel(pc.Fortification) * 500;
        }
        foreach (var n in nation.Navies) upkeep += (n.Warships + n.Transports) * 50;
        foreach (var c in nation.Characters)
            upkeep += (c.CommandSkill + c.AgentSkill + c.EmissarySkill + c.MageSkill) * 20;
        return upkeep;
    }


    // Wiki: 2 por HC/LC (tropa + montura), 1 por resto.
    public static int GetArmyFoodCost(Army army)
    {
        return army.HeavyCavalry * 2 + army.LightCavalry * 2
            + army.HeavyInfantry + army.LightInfantry + army.Archers + army.MenAtArms;
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FASE DE Ã“RDENES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private object ProcessOrder(Order order, Game game)
    {
        var parameters = ParseParameters(order.Parameters);

        // Auto-resolve armyId si no se especificÃ³
        if (order.ArmyId == null && order.Army == null)
        {
            order.Army = order.Nation.Armies.FirstOrDefault(a => a.Id == order.Character?.ArmyId)
                ?? order.Nation.Armies.FirstOrDefault()
                ?? game.Nations.FirstOrDefault(n => n.Id == order.NationId)?.Armies.FirstOrDefault();
            if (order.Army != null)
                order.ArmyId = order.Army.Id;
        }

        var result = order.Code switch
        {
            // â”€â”€ Ã“RDENES ECONÃ“MICAS â”€â”€
            300 => ProcessChangeTaxRate(order, parameters),
            340 => ProcessTransferFoodToArmy(order, parameters, game),
            345 => ProcessTransferFoodToPC(order, parameters, game),

            // â”€â”€ RECLUTAMIENTO â”€â”€
            400 => ProcessRecruit(order, "HeavyCavalry", RecruitCostPerUnit[400], parameters),
            404 => ProcessRecruit(order, "LightCavalry", RecruitCostPerUnit[404], parameters),
            408 => ProcessRecruit(order, "HeavyInfantry", RecruitCostPerUnit[408], parameters),
            412 => ProcessRecruit(order, "LightInfantry", RecruitCostPerUnit[412], parameters),
            416 => ProcessRecruit(order, "Archers", RecruitCostPerUnit[416], parameters),
            420 => ProcessRecruit(order, "MenAtArms", RecruitCostPerUnit[420], parameters),

            // â”€â”€ MOVIMIENTO â”€â”€
            810 => ProcessMoveCharacter(order, parameters, game),
            820 => ProcessMoveCompany(order, parameters, game),
            830 => ProcessMoveNavy(order, parameters, game),
            840 => ProcessStandAndDefend(order, parameters),
            850 => ProcessMoveArmy(order, parameters, game),
            860 => ProcessForceMarch(order, parameters, game),
            870 => ProcessMoveCharacterJoinArmy(order, parameters),

            // â”€â”€ MARKET â”€â”€
            310 => ProcessBidCaravans(order, parameters),
            315 => ProcessPurchaseCaravans(order, parameters),
            320 => ProcessSellCaravans(order, parameters),
            325 => ProcessNationSell(order, parameters),
            960 => ProcessIncreaseCaravanPrices(order, parameters),
            965 => ProcessReduceCaravanPrices(order, parameters),

            // â”€â”€ COMBATE â”€â”€
            230 => ProcessAttack(order, parameters, game),
            235 => ProcessAttackNation(order, parameters, game),
            240 => ProcessDefend(order, parameters),
            250 => ProcessDestroyPC(order, parameters, game),
            255 => ProcessCapturePC(order, parameters, game),
            260 => ProcessSiegePC(order, parameters, game),

            // â”€â”€ ECONOMÃA EXTRA â”€â”€
            347 => ProcessTransferFoodArmyToArmy(order, parameters, game),
            370 => ProcessUpgradeWeapons(order, parameters),
            375 => ProcessUpgradeArmour(order, parameters),

            // â”€â”€ RECLUTAMIENTO EXTRA â”€â”€
            425 => ProcessRetireTroops(order, parameters),
            430 => ProcessTroopsManoeuvres(order, parameters),
            435 => ProcessArmyManoeuvres(order, parameters),
            440 => ProcessMakeWarMachines(order, parameters),
            444 => ProcessMakeArmour(order, parameters),
            448 => ProcessMakeWeapons(order, parameters),

            // â”€â”€ COMPANIES â”€â”€
            745 => ProcessCreateCompany(order, parameters),
            750 => ProcessDisbandCompany(order, parameters),
            755 => ProcessJoinCompany(order, parameters, game),
            780 => ProcessTransferCommand(order, parameters),

            // â”€â”€ HOSTAGES â”€â”€
            620 => ProcessKidnap(order, parameters, game),
            625 => ProcessReleaseHostage(order, parameters),
            630 => ProcessRescueHostage(order, parameters, game),
            635 => ProcessInterrogateHostage(order, parameters),
            640 => ProcessCustodyHostage(order, parameters),
            645 => ProcessImprisonHostage(order, parameters),
            650 => ProcessExecuteHostage(order, parameters),
            655 => ProcessDemandRansom(order, parameters),

            // â”€â”€ SHIPS â”€â”€
            270 => ProcessDestroyShips(order, parameters),
            275 => ProcessScuttleShips(order, parameters),
            280 => ProcessAbandonShips(order, parameters),
            452 => ProcessMakeWarships(order, parameters),
            456 => ProcessMakeTransports(order, parameters),
            794 => ProcessAnchorShips(order, parameters),
            798 => ProcessPickUpShips(order, parameters),

            // â”€â”€ ARTIFACTS â”€â”€
            205 => ProcessUseCombatArtifact(order, parameters),
            360 => ProcessTransferArtifact(order, parameters),
            792 => ProcessDropArtifact(order, parameters),
            796 => ProcessPickUpArtifact(order, parameters),
            805 => ProcessUseMovementArtifact(order, parameters),
            900 => ProcessFindArtifact(order, parameters),
            935 => ProcessUseScryingArtifact(order, parameters),
            945 => ProcessUseHidingArtifact(order, parameters),

            // â”€â”€ MISC â”€â”€
            100 => ProcessHold(order),
            285 => ProcessReactionEncounter(order, parameters),
            290 => ProcessInvestigateEncounter(order, parameters),
            363 => ProcessTransferHostage(order, parameters, game),
            660 => ProcessOfferRansom(order, parameters),
            740 => ProcessRetireCharacter(order, parameters),
            760 => ProcessLeaveCompany(order, parameters),
            765 => ProcessSplitArmy(order, parameters, game),
            785 => ProcessJoinArmy(order, parameters, game),
            790 => ProcessLeaveArmy(order, parameters),
            942 => ProcessMoveTurnMap(order, parameters),
            947 => ProcessNationTransport(order, parameters),
            948 => ProcessTransportCaravan(order, parameters),
            990 => ProcessOneRing(order, parameters),

            // â”€â”€ MAGE SPELLS â”€â”€
            120 => ProcessCastHealSpell(order, parameters),
            225 => ProcessCastCombatSpell(order, parameters),
            330 => ProcessCastConjuringSpell(order, parameters),
            700 => ProcessForgetSpell(order, parameters),
            705 => ProcessResearchSpell(order, parameters),
            710 => ProcessPrenticeMagery(order, parameters),
            825 => ProcessCastMovementSpell(order, parameters),
            940 => ProcessCastLoreSpell(order, parameters, game),

            // â”€â”€ EMISSARY EXTRA â”€â”€
            500 => ProcessRecruitDoubleAgent(order, parameters, game),
            505 => ProcessBribeCharacter(order, parameters, game),
            530 => ProcessImproveHarbour(order, parameters, game),
            535 => ProcessAddHarbour(order, parameters, game),
            550 => ProcessImprovePC(order, parameters, game),
            555 => ProcessCreateCamp(order, parameters, game),
            560 => ProcessAbandonCamp(order, parameters),
            565 => ProcessReducePC(order, parameters),
            580 => ProcessSpreadRumours(order, parameters),
            585 => ProcessUncoverSecrets(order, parameters),

            // â”€â”€ HABILIDADES (resoluciÃ³n genÃ©rica) â”€â”€
            925 => ProcessSkillOrder(order, "Command", parameters),
            600 => ProcessSkillOrder(order, "Agent", parameters),
            910 => ProcessSkillOrder(order, "Agent", parameters),
            915 => ProcessSkillOrder(order, "Agent", parameters),
            520 => ProcessInfluenceOwn(order, parameters),
            525 => ProcessInfluenceOther(order, parameters, game),

            // â”€â”€ Ã“RDENES RECONCILIADAS CON EL REGLAMENTO â”€â”€
            175 => ProcessChangeAllegiance(order, parameters),
            180 => ProcessUpgradeRelations(order, parameters),
            185 => ProcessDowngradeRelations(order, parameters),
            210 => ProcessIssueChallenge(order, parameters, game),
            215 => ProcessRefuseChallenges(order, parameters),
            349 => ProcessTransferWarMachines(order, parameters, game),
            351 => ProcessTransferWeapons(order, parameters, game),
            353 => ProcessTransferArmour(order, parameters, game),
            355 => ProcessTransferTroops(order, parameters, game),
            357 => ProcessTransferShips(order, parameters),
            460 => ProcessRemoveHarbour(order, parameters, game),
            465 => ProcessRemovePort(order, parameters, game),
            470 => ProcessDestroyStores(order, parameters, game),
            475 => ProcessDestroyBridge(order, parameters),
            480 => ProcessRemoveFort(order, parameters, game),
            490 => ProcessBuildBridge(order, parameters),
            494 => ProcessFortifyPC(order, parameters, game),
            498 => ProcessThreatenPC(order, parameters, game),
            552 => ProcessPostCamp(order, parameters, game),
            605 => ProcessGuardLocation(order, parameters, game),
            610 => ProcessGuardCharacter(order, parameters, game),
            615 => ProcessAssassinate(order, parameters, game),
            665 => ProcessSabotageBridge(order, parameters),
            670 => ProcessSabotageFort(order, parameters, game),
            675 => ProcessSabotagePort(order, parameters, game),
            680 => ProcessSabotageProduction(order, parameters, game),
            685 => ProcessStealArtifact(order, parameters, game),
            690 => ProcessStealGold(order, parameters, game),
            725 => ProcessNameCharacter(order, parameters, "commander"),
            728 => ProcessNameCharacter(order, parameters, "commander"),
            731 => ProcessNameCharacter(order, parameters, "agent"),
            734 => ProcessNameCharacter(order, parameters, "emissary"),
            737 => ProcessNameCharacter(order, parameters, "mage"),
            770 => ProcessHireArmy(order, parameters),
            775 => ProcessDisbandArmy(order, parameters),
            905 => ProcessScoutArmy(order, parameters, game),
            920 => ProcessScoutPC(order, parameters, game),
            930 => ProcessScoutCharacters(order, parameters, game),
            949 => ProcessTransferOwnership(order, parameters, game),
            950 => ProcessRelocateCapital(order, parameters, game),

            // â”€â”€ POR DEFECTO: proceso genÃ©rico â”€â”€
            _ => ProcessGenericOrder(order, parameters)
        };

        return result;
    }


    // â”€â”€ Ã“RDENES ESPECÃFICAS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private object ProcessHold(Order order)
    {
        order.Status = "resolved";
        order.Result = "Hold position";
        return MakeResult(order, "Hold position");
    }


    private PopulationCentre? OwnedPCAt(string hex, string nationId)
        => _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex && p.NationId == nationId);


    // Alineamiento del artefacto vs bando de la nación (205/805/935/945).
    private static bool ArtifactUsableBy(Artifact a, Nation n)
    {
        var al = (a.Alignment ?? "").ToLower();
        if (al is "" or "none" or "neutral") return true;
        if (al == "good") return n.Allegiance == "free_peoples";
        if (al == "evil") return n.Allegiance == "dark_servants";
        return true;
    }


    // Hex de la capital de la nación (175/180/185/300/325/280/660/725-737...).
    private static string? CapitalHex(Nation n) =>
        n.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;


    private static bool AtCapital(Order order) =>
        CapitalHex(order.Nation) is { } cap && order.Character?.LocationHex == cap;


    // Tierra firme (no agua/océano) para 552/555/745/910/915/925/930.
    private bool IsLandHex(string gameId, string? hex)
    {
        var tile = TileAt(gameId, hex ?? "");
        return tile != null && tile.Terrain != "water" && tile.Terrain != "ocean";
    }


    // Fuerzas hostiles (relación <= -1 en algún sentido) en el hex.
    private static bool EnemyAtHex(Game game, string? hex, string nationId)
    {
        if (hex == null) return false;
        bool Hostile(string a, string b) => game.Nations.FirstOrDefault(n => n.Id == a)?.Relations
            .FirstOrDefault(r => r.TargetNationId == b)?.Level <= -1;
        return game.Nations
            .SelectMany(n => n.Armies.Select(a => new { a.LocationHex, NationId = n.Id })
                .Concat(n.Navies.Select(v => new { v.LocationHex, NationId = n.Id })))
            .Any(u => u.LocationHex == hex && u.NationId != nationId
                && (Hostile(u.NationId, nationId) || Hostile(nationId, u.NationId)));
    }


    private PopulationCentre? PCAtHex(string hex)
        => _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);


    private int CountTroops(Army a)
        => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms;


    private string GetTerrainAtHex(string hex, Game game)
    {
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r))
            return "plains";
        var tile = _db.HexTiles.FirstOrDefault(h => h.GameId == game.Id && h.Q == q && h.R == r);
        return tile?.Terrain ?? "plains";
    }


    private HexTile? TileAt(string gameId, string hex)
    {
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r))
            return null;
        return _db.HexTiles.FirstOrDefault(t => t.GameId == gameId && t.Q == q && t.R == r);
    }


    private int? SpellFromParameters(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("spell", out var e) && e.ValueKind == JsonValueKind.Number) return e.GetInt32();
            if (doc.RootElement.TryGetProperty("spellNumber", out var e2) && e2.ValueKind == JsonValueKind.Number) return e2.GetInt32();
        }
        catch { }
        return null;
    }


    // Suma de efectos de hechizos de combate para una fuerza (ya Ã·100 si es navÃ­o).
    private (double Off, double Def) GetSpellMods(string? forceId, bool navy)
    {
        double off = 0, def = 0;
        var spells = (navy ? _pendingNavySpells : _pendingArmySpells)
            .TryGetValue(forceId ?? "", out var list) ? list : null;
        if (spells == null) return (0, 0);
        foreach (var sp in spells)
        {
            var e = _combat.GetSpellEffect(sp, navy);
            off += e.Offensive;
            def += e.Defensive;
        }
        return (off, def);
    }


    private static List<string> IdList(Dictionary<string, JsonElement> p, string key)
    {
        var ids = new List<string>();
        if (!p.TryGetValue(key, out var el)) return ids;
        void AddEl(System.Text.Json.JsonElement e)
        {
            if (e.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrEmpty(e.GetString()))
                ids.Add(e.GetString()!);
            else if (e.ValueKind == System.Text.Json.JsonValueKind.Number && e.TryGetInt32(out var n))
                ids.Add(n.ToString());
        }
        if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var e in el.EnumerateArray()) AddEl(e);
        else AddEl(el);
        return ids;
    }


    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  Ã“RDENES RECONCILIADAS CON EL REGLAMENTO (antes placeholder)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private PopulationCentre? GetPC(Game game, string hexOrId) =>
        game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(p => p.Id == hexOrId || p.LocationHex == hexOrId);


    private Army? GetArmyById(Game game, string id) =>
        game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.Id == id);


    private static bool SameOrFriendly(Game game, string myNationId, string otherNationId)
    {
        if (myNationId == otherNationId) return true;
        var rel = game.Nations.FirstOrDefault(n => n.Id == myNationId)?.Relations
            .FirstOrDefault(r => r.TargetNationId == otherNationId);
        return (rel?.Level ?? 0) >= 1;
    }


    private PopulationCentre? ResolvePC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (p.TryGetValue("pcId", out var idEl)) return GetPC(game, idEl.GetString()!);
        if (p.TryGetValue("hex", out var hEl)) return GetPC(game, hEl.GetString()!);
        // Default: PC en la ubicaciÃ³n del ejÃ©rcito/personaje
        var hex = order.Army?.LocationHex ?? order.Character?.LocationHex;
        return hex != null ? GetPC(game, hex) : null;
    }


    private static Dictionary<string, JsonElement> ParseParameters(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value);
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }


    private static object MakeResult(Order order, string message, bool success = true)
    {
        return new
        {
            orderId = order.Id,
            code = order.Code,
            character = order.Character?.Name,
            nation = order.Nation?.Name,
            success,
            message,
            status = order.Status,
            result = order.Result
        };
    }


    private void PersistEncounters(List<Encounter> encounters)
    {
        if (encounters.Count == 0) return;
        _db.Encounters.AddRange(encounters);
    }
}