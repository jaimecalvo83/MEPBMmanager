using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

public class TurnProcessor
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FASE ECONÃ“MICA
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private List<object> ProcessEconomy(Game game, int seasonBonus)
    {
        var results = new List<object>();

        foreach (var nation in game.Nations.Where(n => !n.IsEliminated))
        {
            // Ingreso por PC no asediada: impuesto oficial (tamaño×tasa; camp 0)
            // + oro minado (Production).
            var earners = nation.PopulationCentres.Where(pc => !pc.IsSieged).ToList();
            var pcIncome = earners.Sum(pc => GetPCTax(pc.Size, nation.TaxRate) + Math.Max(0, pc.Production));
            nation.Gold += pcIncome;

            // Mantenimiento oficial (oro): tropas, puertos, barcos, fuertes, pjs.
            var upkeep = NationUpkeep(nation);
            nation.Gold -= upkeep;

            // Sin fondos: subida forzosa de impuestos para cubrir el déficit;
            // por encima de 100 la nación queda eliminada.
            string? forcedNote = null;
            if (nation.Gold < 0)
            {
                int rateBase = earners.Sum(pc => pc.Size.ToLower() switch
                {
                    "city" => 100,
                    "major town" => 75,
                    "town" => 50,
                    "village" => 25,
                    _ => 0
                });
                if (rateBase <= 0)
                {
                    nation.IsEliminated = true;
                    forcedNote = "eliminated (no tax base)";
                }
                else
                {
                    int bump = (int)Math.Ceiling(-nation.Gold / (double)rateBase);
                    int newRate = nation.TaxRate + bump;
                    if (newRate > 100)
                    {
                        nation.IsEliminated = true;
                        forcedNote = $"eliminated (tax rate forced to {newRate}%)";
                    }
                    else
                    {
                        nation.TaxRate = newRate;
                        nation.Gold += rateBase * bump;
                        foreach (var pc in nation.PopulationCentres)
                            pc.Loyalty = Math.Max(1, pc.Loyalty - Math.Max(1, bump / 10));
                        forcedNote = $"forced tax hike to {newRate}%";
                    }
                }
            }

            // Deriva de lealtad por tasa (simplificación documentada): >=50 -2,
            // 30-40 -1, <=20 +1 (tope 100).
            int drift = nation.TaxRate >= 50 ? -2 : nation.TaxRate >= 30 ? -1 : nation.TaxRate <= 20 ? 1 : 0;
            if (drift != 0 && !nation.IsEliminated)
                foreach (var pc in nation.PopulationCentres)
                    pc.Loyalty = Math.Clamp(pc.Loyalty + drift, 1, 100);

            // Consumo de ejÃ©rcitos
            foreach (var army in nation.Armies)
            {
                var foodCost = GetArmyFoodCost(army);
                nation.Food -= foodCost;

                if (nation.Food < 0)
                {
                    // PenalizaciÃ³n por hambre: pierde moral
                    army.Morale = Math.Max(0, army.Morale - 10);
                    nation.Food = 0;
                    results.Add(new
                    {
                        type = "hunger",
                        nation = nation.Name,
                        army = army.Name,
                        message = $"{army.Name} suffers hunger - morale drops"
                    });
                }
            }

            // ProducciÃ³n de recursos bÃ¡sica
            nation.Timber += 50 + seasonBonus;
            nation.Leather += 30 + seasonBonus;
            nation.Bronze += 20;
            nation.Steel += 10;

            results.Add(new
            {
                type = "economy",
                nation = nation.Name,
                gold = nation.Gold,
                food = nation.Food,
                tax = nation.TaxRate,
                message = $"Economy processed: +{pcIncome} income, -{upkeep} upkeep" + (forcedNote != null ? $" ({forcedNote})" : "")
            });
        }

        return results;
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

    private int GetArmyFoodCost(Army army)
    {
        return GetArmyTotalTroops(army) / 100;
    }

    private static int GetArmyTotalTroops(Army army)
    {
        return army.HeavyCavalry + army.LightCavalry
             + army.HeavyInfantry + army.LightInfantry
             + army.Archers + army.MenAtArms;
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
            order.Army = order.Nation.Armies.FirstOrDefault()
                ?? game.Nations.FirstOrDefault(n => n.Id == order.NationId)?.Armies.FirstOrDefault();
            if (order.Army != null)
                order.ArmyId = order.Army.Id;
        }

        var result = order.Code switch
        {
            // â”€â”€ Ã“RDENES ECONÃ“MICAS â”€â”€
            300 => ProcessChangeTaxRate(order, parameters),
            340 => ProcessTransferFoodToArmy(order, parameters),
            345 => ProcessTransferFoodToPC(order, parameters),

            // â”€â”€ RECLUTAMIENTO â”€â”€
            400 => ProcessRecruit(order, "HeavyCavalry", 200, parameters),
            404 => ProcessRecruit(order, "LightCavalry", 120, parameters),
            408 => ProcessRecruit(order, "HeavyInfantry", 150, parameters),
            412 => ProcessRecruit(order, "LightInfantry", 80, parameters),
            416 => ProcessRecruit(order, "Archers", 100, parameters),
            420 => ProcessRecruit(order, "MenAtArms", 60, parameters),

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

            // â”€â”€ COMBATE â”€â”€
            230 => ProcessAttack(order, parameters, game),
            235 => ProcessAttackNation(order, parameters, game),
            240 => ProcessDefend(order, parameters),
            250 => ProcessDestroyPC(order, parameters, game),
            255 => ProcessCapturePC(order, parameters, game),
            260 => ProcessSiegePC(order, parameters, game),

            // â”€â”€ ECONOMÃA EXTRA â”€â”€
            347 => ProcessTransferFoodArmyToArmy(order, parameters),
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
            755 => ProcessJoinCompany(order, parameters),
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
            363 => ProcessTransferHostage(order, parameters),
            660 => ProcessOfferRansom(order, parameters),
            740 => ProcessRetireCharacter(order, parameters),
            760 => ProcessLeaveCompany(order, parameters),
            765 => ProcessSplitArmy(order, parameters),
            785 => ProcessJoinArmy(order, parameters),
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
            500 => ProcessRecruitDoubleAgent(order, parameters),
            505 => ProcessBribeCharacter(order, parameters),
            530 => ProcessImproveHarbour(order, parameters),
            535 => ProcessAddHarbour(order, parameters),
            550 => ProcessImprovePC(order, parameters),
            555 => ProcessCreateCamp(order, parameters),
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
            552 => ProcessPostCamp(order, parameters),
            605 => ProcessGuardLocation(order, parameters),
            610 => ProcessGuardCharacter(order, parameters),
            615 => ProcessAssassinate(order, parameters, game),
            665 => ProcessSabotageBridge(order, parameters),
            670 => ProcessSabotageFort(order, parameters, game),
            675 => ProcessSabotagePort(order, parameters, game),
            680 => ProcessSabotageProduction(order, parameters, game),
            685 => ProcessStealArtifact(order, parameters),
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

    private object ProcessChangeTaxRate(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (parameters.TryGetValue("newRate", out var newRateEl))
        {
            var newRate = newRateEl.GetInt32();
            order.Nation.TaxRate = Math.Clamp(newRate, 10, 80);
            order.Status = "resolved";
            order.Result = $"Tax rate changed to {newRate}%";
            return MakeResult(order, $"Tax rate changed to {order.Nation.TaxRate}%");
        }
        return MakeResult(order, "Invalid parameters for tax rate", false);
    }

    private object ProcessTransferFoodToArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        var army = order.Army;
        if (army == null) return MakeResult(order, "No army specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        var actual = Math.Min(amount, order.Nation.Food);
        order.Nation.Food -= actual;
        army.Food += actual;
        order.Status = "resolved";
        order.Result = $"Transferred {actual} food to {army.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransferFoodToPC(Order order, Dictionary<string, JsonElement> parameters)
    {
        var army = order.Army;
        if (army == null) return MakeResult(order, "No army specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        var actual = Math.Min(amount, army.Food);
        army.Food -= actual;
        order.Nation.Food += actual;
        order.Status = "resolved";
        order.Result = $"Transferred {actual} food from {army.Name} to nation reserve";
        return MakeResult(order, order.Result);
    }

    private Dictionary<string, int> _recruitsUsed = new();
    private readonly HashSet<string> _fortifiedThisTurn = new();

    private PopulationCentre? OwnedPCAt(string hex, string nationId)
        => _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex && p.NationId == nationId);

    public static int RecruitCapacity(string size) => size.ToLower() switch
    {
        "camp" => 100,
        "village" => 200,
        "town" => 300,
        "major town" => 400,
        "city" => 500,
        "fortress" => 500,
        "citadel" => 800,
        _ => 100
    };

    private static readonly string[] SizeOrder = { "camp", "village", "town", "major town", "city", "fortress", "citadel" };

    private PopulationCentre? PCAtHex(string hex)
        => _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);

    private int CountTroops(Army a)
        => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms;

    private object ProcessRecruit(Order order, string troopType, int costPerUnit, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army to recruit into", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var requested = amtEl.GetInt32();
        if (requested <= 0) return MakeResult(order, "Amount must be positive", false);

        var pc = OwnedPCAt(order.Army.LocationHex, order.NationId);
        if (pc == null)
            return MakeResult(order, "Army must be at a population centre you own to recruit", false);

        var capacity = RecruitCapacity(pc.Size);
        var used = _recruitsUsed.GetValueOrDefault(pc.Id);
        var available = Math.Max(0, capacity - used);
        if (available == 0)
            return MakeResult(order, $"No recruits available at {pc.Name} this turn (cap {capacity})", false);

        var needsMount = troopType is "HeavyCavalry" or "LightCavalry";

        var amount = requested;
        if (amount > available) amount = available;
        if (order.Nation.Gold < amount * costPerUnit)
            amount = order.Nation.Gold / costPerUnit;
        if (needsMount && order.Nation.Mounts < amount)
            amount = order.Nation.Mounts;
        if (amount <= 0)
            return MakeResult(order, $"Cannot recruit {troopType}: insufficient gold or mounts", false);

        var totalCost = amount * costPerUnit;
        order.Nation.Gold -= totalCost;
        if (needsMount) order.Nation.Mounts -= amount;

        var existing = CountTroops(order.Army);
        var baseTraining = Math.Max(order.Army.Training, 10);
        // La base nacional (RECRUIT_TRAINING_*) es suelo: manda el mando del general si es mayor.
        var nationBase = NationAbilities.RecruitTrainingFor(order.Nation?.Name, troopType);
        var recruitTraining = Math.Max(nationBase, Math.Clamp(order.Character?.CommandSkill ?? 10, 10, 100));
        switch (troopType)
        {
            case "HeavyCavalry": order.Army.HeavyCavalry += amount; break;
            case "LightCavalry": order.Army.LightCavalry += amount; break;
            case "HeavyInfantry": order.Army.HeavyInfantry += amount; break;
            case "LightInfantry": order.Army.LightInfantry += amount; break;
            case "Archers": order.Army.Archers += amount; break;
            case "MenAtArms": order.Army.MenAtArms += amount; break;
        }
        var newTotal = existing + amount;
        order.Army.Training = existing == 0
            ? recruitTraining
            : (existing * baseTraining + amount * recruitTraining) / newTotal;

        _recruitsUsed[pc.Id] = used + amount;

        order.Status = "resolved";
        var note = amount < requested ? " (limited by availability/gold)" : "";
        order.Result = $"Recruited {amount} {troopType} at {pc.Name} for {totalCost} gold{note}";
        return MakeResult(order, order.Result);
    }

    private object ProcessMoveCharacter(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character to move", false);
        if (!parameters.TryGetValue("destination", out var destEl))
            return MakeResult(order, "No destination specified", false);

        var dest = destEl.GetString()!;
        var evasive = parameters.TryGetValue("evasive", out var evEl) && evEl.GetBoolean();
        var hexes = _db.HexTiles.Where(h => h.GameId == game.Id).ToList();
        var res = _movement.MoveCharacter(order.Character, dest, game, hexes, evasive);
        PersistEncounters(res.Encounters);
        order.Status = "resolved";
        order.Result = res.Message;
        return MakeResult(order, $"Character: {res.Message}");
    }

    private object ProcessMoveArmy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army to move", false);
        if (!parameters.TryGetValue("destination", out var destEl))
            return MakeResult(order, "No destination specified", false);

        var dest = destEl.GetString()!;
        var evasive = parameters.TryGetValue("evasive", out var evEl) && evEl.GetBoolean();
        var hexes = _db.HexTiles.Where(h => h.GameId == game.Id).ToList();
        var res = _movement.MoveArmy(order.Army, order.Character, dest, game, hexes, evasive, forceMarch: false);
        PersistEncounters(res.Encounters);
        order.Status = "resolved";
        order.Result = res.Message;
        return MakeResult(order, $"Army: {res.Message}");
    }

    private object ProcessAttack(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Navy != null) return ProcessNavyAttack(order, parameters, game);
        if (order.Army == null) return MakeResult(order, "No army for attack", false);

        // Buscar ejÃ©rcito enemigo en el mismo hex
        var enemyArmy = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies)
            .FirstOrDefault(a => a.LocationHex == order.Army.LocationHex);

        if (enemyArmy == null)
        {
            order.Status = "resolved";
            order.Result = "No enemy army found at location";
            return MakeResult(order, order.Result);
        }

        var tacticA = parameters.TryGetValue("tactic", out var tEl) ? tEl.GetString() : null;
        var terrain = GetTerrainAtHex(order.Army.LocationHex, game);
        var (aOff, aDef) = GetSpellMods(order.Army.Id, false);
        var (dOff, dDef) = GetSpellMods(enemyArmy.Id, false);
        var battleResult = _combat.ResolveArmyBattle(order.Army, enemyArmy, game, tacticA, null, terrain, aOff, aDef, dOff, dDef);

        CleanupDisbanded(order.Army, game);
        CleanupDisbanded(enemyArmy, game);

        order.Status = "resolved";
        order.Result = battleResult.Message;
        return MakeResult(order, $"Attack: {battleResult.Message} (Attacker lost {battleResult.AttackerCasualties}, Defender lost {battleResult.DefenderCasualties})");
    }

    private object ProcessNavyAttack(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var navy = order.Navy!;
        var tacticA = parameters.TryGetValue("tactic", out var tEl) ? tEl.GetString() : null;
        // Buscar armada enemiga en el mismo hex
        var enemyNavy = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Navies)
            .FirstOrDefault(v => v.LocationHex == navy.LocationHex);

        if (enemyNavy == null)
        {
            // Sin armada enemiga: la armada desembarca (ancla naves, tropas a tierra) para
            // atacar un ejÃ©rcito o centro de poblaciÃ³n enemigo en el hex (reglamento G-4).
            var enemyArmy = game.Nations
                .Where(n => n.Id != order.NationId)
                .SelectMany(n => n.Armies)
                .FirstOrDefault(a => a.LocationHex == navy.LocationHex);
            if (enemyArmy != null)
            {
                var landed = NavyToArmy(navy, game);
                var terrain = GetTerrainAtHex(navy.LocationHex, game);
                var (aOff, aDef) = GetSpellMods(navy.Id, true);
                var (dOff, dDef) = GetSpellMods(enemyArmy.Id, false);
                var res = _combat.ResolveArmyBattle(landed, enemyArmy, game, tacticA, null, terrain, aOff, aDef, dOff, dDef);
                ReflectNavyLanding(navy, landed, game);
                CleanupDisbanded(enemyArmy, game);
                order.Status = "resolved";
                order.Result = $"Navy landed and attacked army: {res.Message}";
                return MakeResult(order, $"Naval landing: {res.Message} (Attacker lost {res.AttackerCasualties}, Defender lost {res.DefenderCasualties})");
            }

            var enemyPC = game.Nations
                .Where(n => n.Id != order.NationId)
                .SelectMany(n => n.PopulationCentres)
                .FirstOrDefault(pc => pc.LocationHex == navy.LocationHex);
            if (enemyPC != null)
            {
                var landed = NavyToArmy(navy, game);
                var terrain = GetTerrainAtHex(navy.LocationHex, game);
                var (aOff, aDef) = GetSpellMods(navy.Id, true);
                var res = _combat.ResolvePopulationCentreAssault(landed, enemyPC, game, "capture", tacticA, terrain, aOff, aDef);
                ReflectNavyLanding(navy, landed, game);
                order.Status = "resolved";
                order.Result = $"Navy landed and assaulted {enemyPC.Name}: {res.Message}";
                return MakeResult(order, $"Naval landing assault: {res.Message} (lost {res.AttackerCasualties} troops)");
            }

            order.Status = "resolved";
            order.Result = "No enemy navy, army or population centre at location";
            return MakeResult(order, order.Result);
        }

        var (naOff, naDef) = GetSpellMods(navy.Id, true);
        var (ndOff, ndDef) = GetSpellMods(enemyNavy.Id, true);
        var battleResult = _combat.ResolveNavyBattle(navy, enemyNavy, game, tacticA, null, naOff, naDef, ndOff, ndDef);

        CleanupDisbandedNavy(navy, game);
        CleanupDisbandedNavy(enemyNavy, game);

        order.Status = "resolved";
        order.Result = battleResult.Message;
        return MakeResult(order, $"Naval attack: {battleResult.Message} (Attacker lost {battleResult.AttackerCasualties}, Defender lost {battleResult.DefenderCasualties})");
    }

    private void CleanupDisbandedNavy(Navy navy, Game game)
    {
        if (navy.Warships + navy.Transports > 0) return;
        var nation = game.Nations.FirstOrDefault(n => n.Id == navy.NationId);
        nation?.Navies.Remove(navy);
        _db.Navies.Remove(navy);
    }

    // Convierte una armada en un ejÃ©rcito episodivo al desembarcar: cada transporte
    // lleva hasta 250 infanterÃ­a (reglamento G-4). Las naves de guerra se anclan.
    private Army NavyToArmy(Navy navy, Game game)
    {
        int infantry = navy.Transports * 250;
        return new Army
        {
            Id = Guid.NewGuid().ToString(),
            NationId = navy.NationId,
            Name = (navy.Nation?.Name ?? "Navy") + " landing force",
            LocationHex = navy.LocationHex,
            CommanderId = navy.CommanderId,
            LightInfantry = infantry,
            Morale = Math.Min(100, navy.Strength),
            Training = 10,
            LIWeaponRank = 10,
            LIArmourRank = 0
        };
    }

    // Refleja las bajas del desembarco en la armada (se pierden transportes proporcionales).
    private void ReflectNavyLanding(Navy navy, Army landed, Game game)
    {
        int initialInf = navy.Transports * 250;
        int remaining = TotalTroops(landed);
        if (initialInf <= 0 || remaining <= 0)
        {
            navy.Warships = 0;
            navy.Transports = 0;
            var nation = game.Nations.FirstOrDefault(n => n.Id == navy.NationId);
            nation?.Navies.Remove(navy);
            _db.Navies.Remove(navy);
            return;
        }
        navy.Transports = (int)Math.Round((double)remaining / 250.0);
    }

    private object ProcessAttackNation(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Navy != null) return ProcessNavyAttack(order, parameters, game);
        if (order.Army == null) return MakeResult(order, "No army for attack", false);

        // Buscar ejÃ©rcito enemigo en el mismo hex
        var enemyArmy = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies)
            .FirstOrDefault(a => a.LocationHex == order.Army.LocationHex);

        if (enemyArmy == null)
        {
            // Ataque a naciÃ³n sin ejÃ©rcito: captura automÃ¡tica
            order.Status = "resolved";
            order.Result = "Nation attacked: no resistance";
            return MakeResult(order, order.Result);
        }

        var tacticA = parameters.TryGetValue("tactic", out var tEl) ? tEl.GetString() : null;
        var terrain = GetTerrainAtHex(order.Army.LocationHex, game);
        var (aOff, aDef) = GetSpellMods(order.Army.Id, false);
        var (dOff, dDef) = GetSpellMods(enemyArmy.Id, false);
        var battleResult = _combat.ResolveArmyBattle(order.Army, enemyArmy, game, tacticA, null, terrain, aOff, aDef, dOff, dDef);

        CleanupDisbanded(order.Army, game);
        CleanupDisbanded(enemyArmy, game);

        order.Status = "resolved";
        order.Result = $"Nation attack: {battleResult.Message}";
        return MakeResult(order, order.Result);
    }

    private static int TotalTroops(Army a)
        => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms;

    private void CleanupDisbanded(Army army, Game game)
    {
        if (TotalTroops(army) > 0) return;
        foreach (var c in army.Characters.ToList()) c.ArmyId = null;
        var nation = game.Nations.FirstOrDefault(n => n.Id == army.NationId);
        nation?.Armies.Remove(army);
        _db.Armies.Remove(army);
    }

    private object ProcessDefend(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army to defend", false);

        order.Army.Morale = Math.Min(100, order.Army.Morale + 5);
        order.Army.Training = Math.Min(20, order.Army.Training + 1);
        order.Status = "resolved";
        order.Result = "Army defending position (+5 morale, +1 training)";
        return MakeResult(order, order.Result);
    }

    private object ProcessCapturePC(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army for capture", false);

        var targetPC = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(pc => pc.LocationHex == order.Army.LocationHex);

        if (targetPC == null)
        {
            order.Status = "resolved";
            order.Result = "No population centre at location";
            return MakeResult(order, order.Result);
        }

        var tactic = parameters.TryGetValue("tactic", out var tEl) ? tEl.GetString() : null;
        var terrain = GetTerrainAtHex(order.Army.LocationHex, game);
        var (aOff, aDef) = GetSpellMods(order.Army.Id, false);
        var result = _combat.ResolvePopulationCentreAssault(order.Army, targetPC, game, "capture", tactic, terrain, aOff, aDef);

        CleanupDisbanded(order.Army, game);
        order.Status = "resolved";
        order.Result = result.Message;
        return MakeResult(order, $"Capture: {result.Message} (lost {result.AttackerCasualties} troops)");
    }

    private object ProcessSiegePC(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army for siege", false);

        var targetPC = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(pc => pc.LocationHex == order.Army.LocationHex);

        if (targetPC == null)
        {
            order.Status = "resolved";
            order.Result = "No population centre to besiege";
            return MakeResult(order, order.Result);
        }

        targetPC.IsSieged = true;
        targetPC.Loyalty = Math.Max(0, targetPC.Loyalty - 20);
        order.Army.Morale = Math.Min(100, order.Army.Morale + 5);
        order.Status = "resolved";
        order.Result = $"Siege established on {targetPC.Name} (loyalty now {targetPC.Loyalty})";
        return MakeResult(order, order.Result);
    }

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

    private object ProcessDestroyPC(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army for destroy", false);

        var targetPC = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(pc => pc.LocationHex == order.Army.LocationHex);

        if (targetPC == null)
        {
            order.Status = "resolved";
            order.Result = "No population centre at location";
            return MakeResult(order, order.Result);
        }

        var tactic = parameters.TryGetValue("tactic", out var tEl) ? tEl.GetString() : null;
        var terrain = GetTerrainAtHex(order.Army.LocationHex, game);
        var (aOff, aDef) = GetSpellMods(order.Army.Id, false);
        var result = _combat.ResolvePopulationCentreAssault(order.Army, targetPC, game, "destroy", tactic, terrain, aOff, aDef);

        CleanupDisbanded(order.Army, game);
        order.Status = "resolved";
        order.Result = result.Message;
        return MakeResult(order, $"Destroy: {result.Message} (lost {result.AttackerCasualties} troops)");
    }

    // â”€â”€ MOVIMIENTO COMPLETO â”€â”€

    private object ProcessMoveCompany(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character == null || order.Character.Company == null)
            return MakeResult(order, "No company to move", false);
        if (!parameters.TryGetValue("destination", out var destEl))
            return MakeResult(order, "No destination specified", false);

        var dest = destEl.GetString()!;
        var hexes = _db.HexTiles.Where(h => h.GameId == game.Id).ToList();
        var res = _movement.MoveCompany(order.Character.Company, dest, game, hexes);
        PersistEncounters(res.Encounters);
        order.Status = "resolved";
        order.Result = res.Message;
        return MakeResult(order, $"Company: {res.Message}");
    }

    private object ProcessMoveNavy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Navy == null) return MakeResult(order, "No navy to move", false);
        if (!parameters.TryGetValue("destination", out var destEl))
            return MakeResult(order, "No destination specified", false);

        var dest = destEl.GetString()!;
        var hexes = _db.HexTiles.Where(h => h.GameId == game.Id).ToList();
        var res = _movement.MoveNavy(order.Navy, dest, game, hexes);
        PersistEncounters(res.Encounters);
        order.Status = "resolved";
        order.Result = res.Message;
        return MakeResult(order, $"Navy: {res.Message}");
    }

    private object ProcessStandAndDefend(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army to stand", false);

        order.Army.Morale = Math.Min(100, order.Army.Morale + 10);
        order.Status = "resolved";
        order.Result = "Army standing and defending";
        return MakeResult(order, order.Result);
    }

    private object ProcessForceMarch(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army to force march", false);
        if (!parameters.TryGetValue("destination", out var destEl))
            return MakeResult(order, "No destination specified", false);

        var dest = destEl.GetString()!;
        var hexes = _db.HexTiles.Where(h => h.GameId == game.Id).ToList();
        // Marcha forzada: ignora la parada forzosa ante enemigos.
        // Moral según habilidad nacional: NONE = 0; resto 1-2 con comida, 2-5 sin ella.
        var res = _movement.MoveArmy(order.Army, order.Character, dest, game, hexes, evasive: false, forceMarch: true);
        int loss = 0;
        if (!NationAbilities.HasForNation(order.Nation?.Name, "FORCE_MARCH_NONE"))
            loss = order.Army.Food > 0 ? _rng.Next(1, 3) : _rng.Next(2, 6);
        if (order.Army != null && loss > 0) order.Army.Morale = Math.Max(0, order.Army.Morale - loss);
        PersistEncounters(res.Encounters);
        order.Status = "resolved";
        order.Result = res.Message + $" (force march, morale -{loss})";
        return MakeResult(order, $"Army: {res.Message} (force march, morale -{loss})");
    }

    private object ProcessMoveCharacterJoinArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (parameters.TryGetValue("destination", out var destEl))
        {
            var dest = destEl.GetString();
            if (order.Character != null) order.Character.LocationHex = dest!;
            if (order.Army != null) order.Army.LocationHex = dest!;
            order.Status = "resolved";
            order.Result = $"Character moved to {dest} and joined army";
            return MakeResult(order, order.Result);
        }
        return MakeResult(order, "No destination specified", false);
    }

    // â”€â”€ MARKET â”€â”€

    private static readonly string[] MarketProducts = { "timber", "leather", "bronze", "steel", "mithril", "mounts", "food" };
    private static readonly Dictionary<string, (int Buy, int Sell)> MarketPrice = new()
    {
        // Precios del Game 299 turno 0 (varían por partida en el reglamento).
        ["timber"] = (12, 8),
        ["leather"] = (8, 5),
        ["bronze"] = (12, 8),
        ["steel"] = (14, 9),
        ["mithril"] = (95, 63),
        ["mounts"] = (22, 15),
        ["food"] = (3, 2)
    };
    private const int MarketSellCap = 20000;   // oro de venta máx por nación y turno
    private static readonly Dictionary<string, int> MarketBuyPool = new()   // uds. por producto y turno
    {
        ["timber"] = 5000,
        ["leather"] = 6000,
        ["bronze"] = 4000,
        ["steel"] = 3000,
        ["mithril"] = 500,
        ["food"] = 24293,
        ["mounts"] = 2000
    };
    private Dictionary<string, int> _nationSellValue = new();
    private Dictionary<string, int> _marketBuyLeft = new();

    private static bool IsMarketProduct(string? p, out string canon)
    {
        canon = (p ?? "").ToLower() switch
        {
            "wood" or "timber" => "timber",
            "leather" => "leather",
            "bronze" => "bronze",
            "steel" => "steel",
            "mithril" => "mithril",
            "mount" or "mounts" or "horse" => "mounts",
            "food" => "food",
            _ => ""
        };
        return !string.IsNullOrEmpty(canon) && MarketPrice.ContainsKey(canon);
    }

    private int GetStock(Nation n, string p) => p switch
    {
        "timber" => n.Timber, "leather" => n.Leather, "bronze" => n.Bronze,
        "steel" => n.Steel, "mithril" => n.Mithril, "mounts" => n.Mounts, "food" => n.Food, _ => 0
    };
    private void AddStock(Nation n, string p, int a) { switch (p) { case "timber": n.Timber += a; break; case "leather": n.Leather += a; break; case "bronze": n.Bronze += a; break; case "steel": n.Steel += a; break; case "mithril": n.Mithril += a; break; case "mounts": n.Mounts += a; break; case "food": n.Food += a; break; } }
    private void RemoveStock(Nation n, string p, int a) { switch (p) { case "timber": n.Timber = Math.Max(0, n.Timber - a); break; case "leather": n.Leather = Math.Max(0, n.Leather - a); break; case "bronze": n.Bronze = Math.Max(0, n.Bronze - a); break; case "steel": n.Steel = Math.Max(0, n.Steel - a); break; case "mithril": n.Mithril = Math.Max(0, n.Mithril - a); break; case "mounts": n.Mounts = Math.Max(0, n.Mounts - a); break; case "food": n.Food = Math.Max(0, n.Food - a); break; } }

    private object ProcessBidCaravans(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("product", out var pEl) || !IsMarketProduct(pEl.GetString(), out var product))
            return MakeResult(order, "No valid product specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var requested = amtEl.GetInt32();
        var (buyPrice, _) = MarketPrice[product];
        var bid = parameters.TryGetValue("price", out var prEl) ? prEl.GetInt32() : buyPrice;
        if (bid < buyPrice) return MakeResult(order, $"Bid {bid} below market buy price {buyPrice}", false);

        var available = _marketBuyLeft.GetValueOrDefault(product);
        var amount = Math.Min(requested, available);
        if (order.Nation.Gold < amount * bid) amount = order.Nation.Gold / bid;
        if (amount <= 0) return MakeResult(order, $"Cannot buy {product}: insufficient gold or sold out", false);

        var cost = amount * bid;
        order.Nation.Gold -= cost;
        if (product == "food" && order.Army != null) order.Army.Food += amount;
        else AddStock(order.Nation, product, amount);
        _marketBuyLeft[product] = available - amount;
        order.Status = "resolved";
        order.Result = $"Bid won: bought {amount} {product} for {cost} gold (bid {bid})";
        return MakeResult(order, order.Result);
    }

    private object ProcessPurchaseCaravans(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("product", out var pEl) || !IsMarketProduct(pEl.GetString(), out var product))
            return MakeResult(order, "No valid product specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var requested = amtEl.GetInt32();
        if (requested <= 0) return MakeResult(order, "Amount must be positive", false);

        var (baseBuy, _) = MarketPrice[product];
        var buyPrice = NationAbilities.MarketBuyPrice(order.Nation?.Name, baseBuy);
        var available = _marketBuyLeft.GetValueOrDefault(product);
        var amount = Math.Min(requested, available);
        if (order.Nation.Gold < amount * buyPrice) amount = order.Nation.Gold / buyPrice;
        if (amount <= 0) return MakeResult(order, $"Cannot buy {product}: insufficient gold or sold out", false);

        var cost = amount * buyPrice;
        order.Nation.Gold -= cost;
        if (product == "food" && order.Army != null) order.Army.Food += amount;
        else AddStock(order.Nation, product, amount);
        _marketBuyLeft[product] = available - amount;
        order.Status = "resolved";
        order.Result = $"Bought {amount} {product} from market for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessSellCaravans(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("product", out var pEl) || !IsMarketProduct(pEl.GetString(), out var product))
            return MakeResult(order, "No valid product specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var requested = amtEl.GetInt32();
        if (requested <= 0) return MakeResult(order, "Amount must be positive", false);

        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex != null)
        {
            var pc = OwnedPCAt(hex, order.NationId);
            if (pc == null || pc.IsSieged)
                return MakeResult(order, "Must be at your own non-sieged population centre to sell", false);
        }

        var (_, baseSell) = MarketPrice[product];
        var sellPrice = NationAbilities.MarketSellPrice(order.Nation?.Name, baseSell);
        var stock = GetStock(order.Nation, product);
        var amount = Math.Min(requested, stock);
        var value = amount * sellPrice;
        var spent = _nationSellValue.GetValueOrDefault(order.NationId);
        if (spent + value > MarketSellCap)
        {
            amount = (MarketSellCap - spent) / sellPrice;
            value = amount * sellPrice;
        }
        if (amount <= 0) return MakeResult(order, $"Sell limit reached this turn ({MarketSellCap} gold)", false);

        RemoveStock(order.Nation, product, amount);
        order.Nation.Gold += value;
        _nationSellValue[order.NationId] = spent + value;
        order.Status = "resolved";
        order.Result = $"Sold {amount} {product} for {value} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessNationSell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("product", out var pEl) || !IsMarketProduct(pEl.GetString(), out var product))
            return MakeResult(order, "No valid product specified", false);
        var pct = parameters.TryGetValue("percentage", out var pe) ? Math.Clamp(pe.GetInt32(), 0, 100) : 100;

        var (_, baseSellAll) = MarketPrice[product];
        var sellPrice = NationAbilities.MarketSellPrice(order.Nation?.Name, baseSellAll);
        var stock = GetStock(order.Nation, product);
        var amount = stock * pct / 100;
        var value = amount * sellPrice;
        var spent = _nationSellValue.GetValueOrDefault(order.NationId);
        if (spent + value > MarketSellCap)
        {
            amount = (MarketSellCap - spent) / sellPrice;
            value = amount * sellPrice;
        }
        if (amount <= 0) return MakeResult(order, $"Sell limit reached this turn ({MarketSellCap} gold)", false);

        RemoveStock(order.Nation, product, amount);
        order.Nation.Gold += value;
        _nationSellValue[order.NationId] = spent + value;
        order.Status = "resolved";
        order.Result = $"Nation sold {amount} {product} ({pct}%) for {value} gold";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ ECONOMÃA EXTRA â”€â”€

    private object ProcessTransferFoodArmyToArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        var src = order.Army;
        if (src == null) return MakeResult(order, "No source army", false);
        if (!parameters.TryGetValue("targetArmyId", out var tgtEl) || !parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "Missing targetArmyId or amount", false);
        var tgt = _db.Armies.Find(tgtEl.GetString());
        if (tgt == null) return MakeResult(order, "Target army not found", false);
        var amount = Math.Min(amtEl.GetInt32(), src.Food);
        src.Food -= amount;
        tgt.Food += amount;
        order.Status = "resolved";
        order.Result = $"Transferred {amount} food from {src.Name} to {tgt.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessUpgradeWeapons(Order order, Dictionary<string, JsonElement> parameters)
    {
        var cost = 500;
        if (order.Nation.Gold < cost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {cost}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= cost;
        order.Status = "resolved";
        order.Result = $"Weapons upgraded for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessUpgradeArmour(Order order, Dictionary<string, JsonElement> parameters)
    {
        var cost = 600;
        if (order.Nation.Gold < cost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {cost}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= cost;
        order.Status = "resolved";
        order.Result = $"Armour upgraded for {cost} gold";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ RECLUTAMIENTO EXTRA â”€â”€

    private object ProcessRetireTroops(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);

        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        ReduceTroops(order.Army, amount);
        order.Nation.Gold += amount * 10;
        order.Status = "resolved";
        order.Result = $"Retired {amount} troops for {amount * 10} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessTroopsManoeuvres(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);

        order.Army.Morale = Math.Min(100, order.Army.Morale + 5);
        order.Character.CommandSkill += 1;
        order.Status = "resolved";
        order.Result = "Troops on manoeuvres (+5 morale, +1 command)";
        return MakeResult(order, order.Result);
    }

    private object ProcessArmyManoeuvres(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);

        order.Army.Morale = Math.Min(100, order.Army.Morale + 10);
        order.Character.CommandSkill += 1;
        order.Status = "resolved";
        order.Result = "Army on manoeuvres (+10 morale, +1 command)";
        return MakeResult(order, order.Result);
    }

    private object ProcessMakeWarMachines(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var amount = amtEl.GetInt32();
        if (amount <= 0) return MakeResult(order, "Amount must be positive", false);

        var pc = OwnedPCAt(order.Army.LocationHex, order.NationId);
        if (pc == null)
            return MakeResult(order, "Must be at a population centre you own to build war machines", false);

        var goldCost = amount * 50;
        var timberCost = amount * 10;
        var steelCost = amount * 5;
        if (order.Nation.Gold < goldCost || order.Nation.Timber < timberCost || order.Nation.Steel < steelCost)
            return MakeResult(order, $"Insufficient resources for {amount} war machines (need {goldCost}g, {timberCost} timber, {steelCost} steel)", false);

        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= timberCost;
        order.Nation.Steel -= steelCost;
        order.Army.WarMachines += amount;
        order.Status = "resolved";
        order.Result = $"Built {amount} war machines at {pc.Name} for {goldCost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessMakeArmour(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var amount = amtEl.GetInt32();
        if (amount <= 0) return MakeResult(order, "Amount must be positive", false);

        var pc = OwnedPCAt(order.Army.LocationHex, order.NationId);
        if (pc == null)
            return MakeResult(order, "Must be at a population centre you own to forge armour", false);

        var army = order.Army;
        var maxAdd = Math.Min(amount, 100 - army.HCArmourRank);
        if (maxAdd <= 0) return MakeResult(order, "Armour already at maximum rank (100)", false);

        var goldCost = maxAdd * 5;
        var leatherCost = maxAdd * 5;
        var steelCost = maxAdd * 2;
        if (order.Nation.Gold < goldCost || order.Nation.Leather < leatherCost || order.Nation.Steel < steelCost)
            return MakeResult(order, $"Insufficient resources to improve armour (need {goldCost}g, {leatherCost} leather, {steelCost} steel)", false);

        order.Nation.Gold -= goldCost;
        order.Nation.Leather -= leatherCost;
        order.Nation.Steel -= steelCost;
        army.HCArmourRank += maxAdd;
        army.LCArmourRank += maxAdd;
        army.HIArmourRank += maxAdd;
        army.LIArmourRank += maxAdd;
        army.ArcherArmourRank += maxAdd;
        army.MAAArmourRank += maxAdd;
        order.Status = "resolved";
        order.Result = $"Improved armour rank by {maxAdd} (now {army.HCArmourRank}) at {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessMakeWeapons(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var amount = amtEl.GetInt32();
        if (amount <= 0) return MakeResult(order, "Amount must be positive", false);

        var pc = OwnedPCAt(order.Army.LocationHex, order.NationId);
        if (pc == null)
            return MakeResult(order, "Must be at a population centre you own to forge weapons", false);

        var army = order.Army;
        var maxAdd = Math.Min(amount, 100 - army.HCWeaponRank);
        if (maxAdd <= 0) return MakeResult(order, "Weapons already at maximum rank (100)", false);

        var goldCost = maxAdd * 5;
        var bronzeCost = maxAdd * 3;
        var steelCost = maxAdd * 1;
        if (order.Nation.Gold < goldCost || order.Nation.Bronze < bronzeCost || order.Nation.Steel < steelCost)
            return MakeResult(order, $"Insufficient resources to improve weapons (need {goldCost}g, {bronzeCost} bronze, {steelCost} steel)", false);

        order.Nation.Gold -= goldCost;
        order.Nation.Bronze -= bronzeCost;
        order.Nation.Steel -= steelCost;
        army.HCWeaponRank += maxAdd;
        army.LCWeaponRank += maxAdd;
        army.HIWeaponRank += maxAdd;
        army.LIWeaponRank += maxAdd;
        army.ArcherWeaponRank += maxAdd;
        army.MAAWeaponRank += maxAdd;
        order.Status = "resolved";
        order.Result = $"Improved weapon rank by {maxAdd} (now {army.HCWeaponRank}) at {pc.Name}";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ COMPANIES â”€â”€

    private object ProcessCreateCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("name", out var nameEl))
            return MakeResult(order, "No company name specified", false);

        var companyName = nameEl.GetString() ?? "New Company";
        var cost = 500;

        if (order.Nation.Gold < cost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {cost}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= cost;

        var company = new Company
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = companyName,
            LocationHex = order.Character?.LocationHex ?? "0,0"
        };
        _db.Companies.Add(company);

        if (order.Character != null)
            order.Character.CompanyId = company.Id;

        order.Status = "resolved";
        order.Result = $"Company '{companyName}' created for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessDisbandCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character?.CompanyId == null)
            return MakeResult(order, "Character is not in a company", false);

        var company = _db.Companies.Find(order.Character.CompanyId);
        if (company != null)
        {
            foreach (var member in _db.Characters.Where(c => c.CompanyId == company.Id))
                member.CompanyId = null;
            _db.Companies.Remove(company);
        }

        order.Status = "resolved";
        order.Result = "Company disbanded";
        return MakeResult(order, order.Result);
    }

    private object ProcessJoinCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("companyId", out var cidEl))
            return MakeResult(order, "No company ID specified", false);

        var companyId = cidEl.GetString();
        var company = _db.Companies.Find(companyId);
        if (company == null)
            return MakeResult(order, "Company not found", false);

        if (order.Character != null)
            order.Character.CompanyId = companyId;

        order.Status = "resolved";
        order.Result = $"Joined company '{company.Name}'";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransferCommand(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tEl))
            return MakeResult(order, "Missing targetId", false);
        var target = _db.Characters.Find(tEl.GetString());
        var army = order.Army
                   ?? (parameters.TryGetValue("armyId", out var aEl) ? _db.Armies.Find(aEl.GetString()) : null);
        if (army == null) return MakeResult(order, "No army to transfer command of", false);
        if (target == null) return MakeResult(order, "Target commander not found", false);
        army.CommanderId = target.Id;
        target.ArmyId ??= army.Id;
        army.Morale = Math.Max(0, army.Morale - 5);
        order.Status = "resolved";
        order.Result = $"Command of {army.Name} transferred to {target.Name}";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ HOSTAGES â”€â”€

    private object ProcessKidnap(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null)
            return MakeResult(order, "Target not found", false);

        if (target.IsDead || target.IsKidnapped)
            return MakeResult(order, "Target cannot be kidnapped", false);

        var roll = NationAbilities.AssassinSkill(order.Nation?.Name, order.Character.AgentSkill) + _rng.Next(1, 7);
        var targetDefense = target.CommandSkill / 2 + _rng.Next(1, 7);
        var success = roll > targetDefense;

        if (success)
        {
            target.IsKidnapped = true;
            target.HeldByNationId = order.NationId;
            order.Status = "resolved";
            order.Result = $"Kidnap successful (roll {roll} vs {targetDefense}): {target.Name} captured";
        }
        else
        {
            order.Character.AgentSkill = Math.Max(0, order.Character.AgentSkill - 1);
            order.Status = "resolved";
            order.Result = $"Kidnap failed (roll {roll} vs {targetDefense}): {target.Name} escaped";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessReleaseHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        target.IsKidnapped = false;
        target.HeldByNationId = null;
        order.Status = "resolved";
        order.Result = $"Released {target.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessRescueHostage(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        var roll = order.Character.AgentSkill + _rng.Next(1, 7);
        var difficulty = 12;
        var success = roll >= difficulty;

        if (success)
        {
            target.IsKidnapped = false;
            target.HeldByNationId = null;
            order.Character.AgentSkill += 1;
            order.Status = "resolved";
            order.Result = $"Rescue successful (roll {roll}): {target.Name} freed";
        }
        else
        {
            order.Character.Health -= 20;
            order.Status = "resolved";
            order.Result = $"Rescue failed (roll {roll}): {target.Name} still captive";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessInterrogateHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        var roll = order.Character.AgentSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (success)
        {
            order.Character.AgentSkill += 1;
            order.Status = "resolved";
            order.Result = $"Interrogation successful (roll {roll}): intelligence gathered";
        }
        else
        {
            target.Health -= 10;
            order.Status = "resolved";
            order.Result = $"Interrogation failed (roll {roll}): hostage uncooperative";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessCustodyHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        if (order.Army != null && order.Character != null)
            target.LocationHex = order.Army.LocationHex;

        order.Status = "resolved";
        order.Result = $"Took custody of {target.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessImprisonHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        target.Health = Math.Max(10, target.Health - 30);
        order.Status = "resolved";
        order.Result = $"{target.Name} imprisoned (health -30)";
        return MakeResult(order, order.Result);
    }

    private object ProcessExecuteHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null)
            return MakeResult(order, "Target not found", false);

        target.IsDead = true;
        target.IsKidnapped = false;
        target.Health = 0;
        order.Character.ChallengeRank += 3;
        order.Status = "resolved";
        order.Result = $"{target.Name} executed (+3 challenge rank)";
        return MakeResult(order, order.Result);
    }

    private object ProcessDemandRansom(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var targetEl))
            return MakeResult(order, "No target specified", false);

        var targetId = targetEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        if (target.HeldByNationId != order.NationId)
            return MakeResult(order, "Target is not held by your nation", false);

        var ransomAmount = target.Type switch
        {
            "commander" => 2000,
            "agent" => 1500,
            "emissary" => 1500,
            "mage" => 2500,
            _ => 1000
        };

        order.Nation.Gold += ransomAmount;
        target.IsKidnapped = false;
        target.HeldByNationId = null;
        order.Status = "resolved";
        order.Result = $"Ransom demanded: {target.Name} released for {ransomAmount} gold";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ SHIPS â”€â”€

    private object ProcessDestroyShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        var navy = order.Nation.Navies.FirstOrDefault();
        if (navy == null) return MakeResult(order, "No navy", false);

        var destroyed = Math.Min(amount, navy.Warships);
        navy.Warships -= destroyed;
        order.Status = "resolved";
        order.Result = $"Destroyed {destroyed} warships";
        return MakeResult(order, order.Result);
    }

    private object ProcessScuttleShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        var navy = order.Nation.Navies.FirstOrDefault();
        if (navy == null) return MakeResult(order, "No navy", false);

        var scuttled = Math.Min(amount, navy.Transports);
        navy.Transports -= scuttled;
        order.Status = "resolved";
        order.Result = $"Scuttled {scuttled} transports";
        return MakeResult(order, order.Result);
    }

    private object ProcessAbandonShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        var navy = order.Nation.Navies.FirstOrDefault();
        if (navy == null) return MakeResult(order, "No navy", false);

        navy.Warships = 0;
        navy.Transports = 0;
        order.Status = "resolved";
        order.Result = "All ships abandoned";
        return MakeResult(order, order.Result);
    }

    private object ProcessMakeWarships(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var amount = amtEl.GetInt32();
        if (amount <= 0) return MakeResult(order, "Amount must be positive", false);

        var hex = order.Army?.LocationHex ?? order.Character?.LocationHex ?? order.Navy?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for shipbuilding", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null || (!pc.HasPort && !pc.HasHarbour))
            return MakeResult(order, "Must be at a coastal population centre (port/harbour) you own to build ships", false);

        // Reglamento: 1500 madera + 1000 oro por buque de guerra (dto. nacional
        // en madera); con poco material se construyen los que se pueda.
        // (La madera sale de la reserva nacional, no de stores del PC.)
        const int goldPerWarship = 1000;
        var timberPerWarship = NationAbilities.ShipTimberCost(order.Nation?.Name);
        amount = Math.Min(amount, Math.Min(order.Nation.Timber / timberPerWarship, order.Nation.Gold / goldPerWarship));
        if (amount <= 0)
            return MakeResult(order, $"Insufficient resources for warships (need {goldPerWarship}g, {timberPerWarship} timber each)", false);

        var goldCost = amount * goldPerWarship;
        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= amount * timberPerWarship;
        var navy = order.Nation.Navies.FirstOrDefault(n => n.LocationHex == hex);
        if (navy == null)
        {
            navy = new Navy { Id = Guid.NewGuid().ToString(), NationId = order.NationId, LocationHex = hex };
            _db.Navies.Add(navy);
        }
        navy.Warships += amount;
        order.Status = "resolved";
        order.Result = $"Built {amount} warships at {pc.Name} for {goldCost} gold and {amount * timberPerWarship} timber";
        return MakeResult(order, order.Result);
    }

    private object ProcessMakeTransports(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);
        var amount = amtEl.GetInt32();
        if (amount <= 0) return MakeResult(order, "Amount must be positive", false);

        var hex = order.Army?.LocationHex ?? order.Character?.LocationHex ?? order.Navy?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for shipbuilding", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null || (!pc.HasPort && !pc.HasHarbour))
            return MakeResult(order, "Must be at a coastal population centre (port/harbour) you own to build ships", false);

        // Reglamento: igual que guerra (1500 + 1000, dto. nacional en madera).
        const int goldPerTransport = 1000;
        var timberPerTransport = NationAbilities.ShipTimberCost(order.Nation?.Name);
        amount = Math.Min(amount, Math.Min(order.Nation.Timber / timberPerTransport, order.Nation.Gold / goldPerTransport));
        if (amount <= 0)
            return MakeResult(order, $"Insufficient resources for transports (need {goldPerTransport}g, {timberPerTransport} timber each)", false);

        var goldCost = amount * goldPerTransport;
        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= amount * timberPerTransport;
        var navy = order.Nation.Navies.FirstOrDefault(n => n.LocationHex == hex);
        if (navy == null)
        {
            navy = new Navy { Id = Guid.NewGuid().ToString(), NationId = order.NationId, LocationHex = hex };
            _db.Navies.Add(navy);
        }
        navy.Transports += amount;
        order.Status = "resolved";
        order.Result = $"Built {amount} transports at {pc.Name} for {goldCost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessAnchorShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Ships anchored at port";
        return MakeResult(order, order.Result);
    }

    private object ProcessPickUpShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var amount = amtEl.GetInt32();
        var navy = order.Nation.Navies.FirstOrDefault();
        if (navy != null) navy.Transports += amount;

        order.Status = "resolved";
        order.Result = $"Picked up {amount} ships";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ ARTIFACTS â”€â”€

    private object ProcessUseCombatArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var artifact = order.Character?.Artifacts.FirstOrDefault(a => a.Type == "combat");
        if (artifact == null)
            return MakeResult(order, "No combat artifact available", false);

        order.Character!.CommandSkill += artifact.Bonus;
        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: +{artifact.Bonus} command";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransferArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl) || !parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "Missing artifactId or targetId", false);

        var artifactId = artEl.GetString();
        var targetId = tgtEl.GetString();
        var artifact = _db.Artifacts.Find(artifactId);
        var target = _db.Characters.Find(targetId);

        if (artifact == null || target == null)
            return MakeResult(order, "Artifact or target not found", false);

        artifact.HeldByCharacterId = targetId;
        order.Status = "resolved";
        order.Result = $"Transferred {artifact.Name} to {target.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessDropArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "No artifact specified", false);

        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null) return MakeResult(order, "Artifact not found", false);

        artifact.HeldByCharacterId = null;
        artifact.LocationHex = order.Character?.LocationHex;
        order.Status = "resolved";
        order.Result = $"Dropped {artifact.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessPickUpArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "No artifact specified", false);

        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null) return MakeResult(order, "Artifact not found", false);

        if (order.Character != null)
            artifact.HeldByCharacterId = order.Character.Id;

        order.Status = "resolved";
        order.Result = $"Picked up {artifact.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessUseMovementArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var artifact = order.Character?.Artifacts.FirstOrDefault(a => a.Type == "movement");
        if (artifact == null)
            return MakeResult(order, "No movement artifact available", false);

        if (parameters.TryGetValue("destination", out var destEl))
        {
            var dest = destEl.GetString();
            if (order.Army != null) order.Army.LocationHex = dest!;
            if (order.Character != null) order.Character.LocationHex = dest!;
        }

        order.Status = "resolved";
        order.Result = $"Used {artifact.Name} for movement";
        return MakeResult(order, order.Result);
    }

    private object ProcessFindArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var roll = order.Character?.AgentSkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 14;

        if (success)
        {
            var artifact = new Artifact
            {
                Id = Guid.NewGuid().ToString(),
                NationId = order.NationId,
                Name = "Found Artifact",
                Type = "misc",
                Bonus = _rng.Next(1, 4),
                LocationHex = order.Character?.LocationHex,
                HeldByCharacterId = order.Character?.Id
            };
            _db.Artifacts.Add(artifact);
            order.Status = "resolved";
            order.Result = $"Found artifact (roll {roll})! Bonus +{artifact.Bonus}";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"No artifact found (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessUseScryingArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var artifact = order.Character?.Artifacts.FirstOrDefault(a => a.Type == "scrying");
        if (artifact == null)
            return MakeResult(order, "No scrying artifact available", false);

        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: scrying active";
        return MakeResult(order, order.Result);
    }

    private object ProcessUseHidingArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var artifact = order.Character?.Artifacts.FirstOrDefault(a => a.Type == "hiding");
        if (artifact == null)
            return MakeResult(order, "No hiding artifact available", false);

        if (order.Character != null)
            order.Character.Stealth += 10;

        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: +10 stealth";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ MISC â”€â”€

    private object ProcessReactionEncounter(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var enc = _db.Encounters.FirstOrDefault(e => !e.IsResolved && e.LocationHex == ch.LocationHex &&
                                                     (e.CharacterId == ch.Id || e.CharacterId == null));
        if (enc == null) return MakeResult(order, "No encounter to react to here", false);

        int roll = ch.CommandSkill + _rng.Next(1, 7);
        enc.IsResolved = true;
        if (roll >= 10)
        {
            int gold = _rng.Next(50, 300);
            order.Nation.Gold += gold;
            enc.Result = $"Defeated the {enc.Type} (roll {roll}); gained {gold} gold";
        }
        else
        {
            int dmg = _rng.Next(10, 40);
            ch.Health = Math.Max(0, ch.Health - dmg);
            if (ch.Health <= 0) { ch.IsDead = true; ch.ArmyId = null; }
            enc.Result = $"Overwhelmed by the {enc.Type} (roll {roll}); took {dmg} damage";
        }
        order.Status = "resolved";
        order.Result = enc.Result;
        return MakeResult(order, enc.Result);
    }

    private object ProcessInvestigateEncounter(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var enc = _db.Encounters.FirstOrDefault(e => !e.IsResolved && e.LocationHex == ch.LocationHex &&
                                                     (e.CharacterId == ch.Id || e.CharacterId == null));
        if (enc == null) return MakeResult(order, "No encounter to investigate here", false);

        int roll = ch.AgentSkill + _rng.Next(1, 7);
        enc.IsResolved = true;
        if (roll < 10)
        {
            enc.Result = $"Investigation failed (roll {roll})";
            order.Status = "resolved";
            order.Result = enc.Result;
            return MakeResult(order, enc.Result);
        }

        switch (enc.Type)
        {
            case "artifact":
                _db.Artifacts.Add(new Artifact
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = order.NationId,
                    Name = "Recovered Artifact",
                    Type = "combat",
                    Alignment = "none",
                    Bonus = _rng.Next(100, 500),
                    IsAtCapital = false,
                    HeldByCharacterId = ch.Id,
                });
                enc.Result = $"Found an artifact (roll {roll})";
                break;
            case "lore":
                if (ch.MageSkill > 0)
                    _db.Spells.Add(new Spell
                    {
                        Id = Guid.NewGuid().ToString(),
                        CharacterId = ch.Id,
                        SpellId = _rng.Next(120, 340),
                        IsKnown = true,
                    });
                enc.Result = $"Learned lore (roll {roll})";
                break;
            case "creature":
                order.Nation.Gold += _rng.Next(20, 150);
                enc.Result = $"Bested the creature (roll {roll})";
                break;
            default:
                enc.Result = $"Investigation successful (roll {roll})";
                break;
        }
        order.Status = "resolved";
        order.Result = enc.Result;
        return MakeResult(order, enc.Result);
    }

    private object ProcessTransferHostage(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "No target specified", false);

        var target = _db.Characters.Find(tgtEl.GetString());
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        if (order.Character != null)
            target.LocationHex = order.Character.LocationHex;

        order.Status = "resolved";
        order.Result = $"Transferred hostage {target.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessOfferRansom(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "No target specified", false);

        var target = _db.Characters.Find(tgtEl.GetString());
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);

        var amount = parameters.TryGetValue("amount", out var amtEl) ? amtEl.GetInt32() : 1000;
        order.Nation.Gold -= Math.Min(amount, order.Nation.Gold);
        target.IsKidnapped = false;
        target.HeldByNationId = null;

        order.Status = "resolved";
        order.Result = $"Ransom offered: {target.Name} freed for {amount} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessRetireCharacter(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);

        order.Character.IsDead = true;
        order.Character.Health = 0;
        order.Status = "resolved";
        order.Result = $"{order.Character.Name} retired";
        return MakeResult(order, order.Result);
    }

    private object ProcessLeaveCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character?.CompanyId == null)
            return MakeResult(order, "Not in a company", false);

        order.Character.CompanyId = null;
        order.Status = "resolved";
        order.Result = "Left company";
        return MakeResult(order, order.Result);
    }

    private object ProcessSplitArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army to split", false);

        var splitRatio = 0.5;
        var newArmy = new Army
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = $"{order.Army.Name} (Split)",
            LocationHex = order.Army.LocationHex,
            HeavyCavalry = (int)(order.Army.HeavyCavalry * splitRatio),
            LightCavalry = (int)(order.Army.LightCavalry * splitRatio),
            HeavyInfantry = (int)(order.Army.HeavyInfantry * splitRatio),
            LightInfantry = (int)(order.Army.LightInfantry * splitRatio),
            Archers = (int)(order.Army.Archers * splitRatio),
            MenAtArms = (int)(order.Army.MenAtArms * splitRatio),
            HCWeaponRank = order.Army.HCWeaponRank,
            HCArmourRank = order.Army.HCArmourRank,
            LCWeaponRank = order.Army.LCWeaponRank,
            LCArmourRank = order.Army.LCArmourRank,
            HIWeaponRank = order.Army.HIWeaponRank,
            HIArmourRank = order.Army.HIArmourRank,
            LIWeaponRank = order.Army.LIWeaponRank,
            LIArmourRank = order.Army.LIArmourRank,
            ArcherWeaponRank = order.Army.ArcherWeaponRank,
            ArcherArmourRank = order.Army.ArcherArmourRank,
            MAAWeaponRank = order.Army.MAAWeaponRank,
            MAAArmourRank = order.Army.MAAArmourRank,
            Morale = order.Army.Morale
        };

        order.Army.HeavyCavalry -= newArmy.HeavyCavalry;
        order.Army.LightCavalry -= newArmy.LightCavalry;
        order.Army.HeavyInfantry -= newArmy.HeavyInfantry;
        order.Army.LightInfantry -= newArmy.LightInfantry;
        order.Army.Archers -= newArmy.Archers;
        order.Army.MenAtArms -= newArmy.MenAtArms;

        _db.Armies.Add(newArmy);
        order.Status = "resolved";
        order.Result = $"Army split: {newArmy.Name} created";
        return MakeResult(order, order.Result);
    }

    private object ProcessJoinArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("armyId", out var armyEl))
            return MakeResult(order, "No army specified", false);

        var army = _db.Armies.Find(armyEl.GetString());
        if (army == null) return MakeResult(order, "Army not found", false);

        if (order.Character != null)
        {
            order.Character.ArmyId = army.Id;
            order.Character.LocationHex = army.LocationHex;
        }

        order.Status = "resolved";
        order.Result = $"Joined army {army.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessLeaveArmy(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character?.ArmyId == null)
            return MakeResult(order, "Not in an army", false);

        order.Character.ArmyId = null;
        order.Status = "resolved";
        order.Result = "Left army";
        return MakeResult(order, order.Result);
    }

    private object ProcessMoveTurnMap(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Turn map moved";
        return MakeResult(order, order.Result);
    }

    private object ProcessNationTransport(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("resource", out var resEl) || !parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "Missing resource or amount", false);

        var resource = resEl.GetString();
        var amount = amtEl.GetInt32();

        switch (resource)
        {
            case "food": order.Nation.Food += amount; break;
            case "timber": order.Nation.Timber += amount; break;
            case "leather": order.Nation.Leather += amount; break;
            case "bronze": order.Nation.Bronze += amount; break;
            case "steel": order.Nation.Steel += amount; break;
        }

        order.Status = "resolved";
        order.Result = $"Transported {amount} {resource}";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransportCaravan(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("resource", out var resEl) || !parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "Missing resource or amount", false);

        var resource = resEl.GetString();
        var amount = amtEl.GetInt32();
        var cost = amount * 5;

        if (order.Nation.Gold < cost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {cost}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= cost;
        switch (resource)
        {
            case "food": order.Nation.Food += amount; break;
            case "timber": order.Nation.Timber += amount; break;
            case "leather": order.Nation.Leather += amount; break;
            case "bronze": order.Nation.Bronze += amount; break;
            case "steel": order.Nation.Steel += amount; break;
        }

        order.Status = "resolved";
        order.Result = $"Caravan transported {amount} {resource} for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessOneRing(Order order, Dictionary<string, JsonElement> parameters)
    {
        var roll = _rng.Next(1, 7);
        var success = roll >= 6;
        order.Status = "resolved";
        order.Result = success
            ? $"The One Ring obeys! (roll {roll})"
            : $"The One Ring resists... (roll {roll})";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ MAGE EXTRA â”€â”€

    private object ProcessCastHealSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Heal))
            return SpellUnknown(order, SpellType.Heal);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 8;

        if (success && parameters.TryGetValue("targetId", out var tgtEl))
        {
            var target = _db.Characters.Find(tgtEl.GetString());
            if (target != null)
            {
                var healAmount = _rng.Next(20, 50);
                target.Health = Math.Min(target.MaxHealth, target.Health + healAmount);
                order.Status = "resolved";
                order.Result = $"Heal successful (roll {roll}): {target.Name} healed {healAmount} HP";
                return MakeResult(order, order.Result);
            }
        }

        order.Status = "resolved";
        order.Result = $"Heal failed (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessCastConjuringSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Conjuring))
            return SpellUnknown(order, SpellType.Conjuring);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (success)
        {
            var amount = _rng.Next(100, 500);
            order.Nation.Gold += amount;
            order.Status = "resolved";
            order.Result = $"Conjuring successful (roll {roll}): created {amount} gold";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"Conjuring failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessCastMovementSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Movement))
            return SpellUnknown(order, SpellType.Movement);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (success && parameters.TryGetValue("destination", out var destEl))
        {
            var dest = destEl.GetString();
            if (order.Army != null) order.Army.LocationHex = dest!;
            if (order.Character != null) order.Character.LocationHex = dest!;
            order.Status = "resolved";
            order.Result = $"Movement spell cast successfully (roll {roll}), moved to {dest}";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"Movement spell failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessResearchSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No mage for research", false);

        // Hechizo objetivo: parámetro spellId o uno aleatorio aún no conocido.
        // Los perdidos exigen acceso nacional (LOST_SPELL_<id>).
        SpellDefinition def;
        if (parameters.TryGetValue("spellId", out var sidEl) && SpellCatalog.Get(sidEl.GetInt32()) is { } byId)
        {
            def = byId;
            if (def.IsLost && !NationAbilities.CanLearnLostSpell(order.Nation?.Name, def.Id))
                return MakeResult(order, $"Lost spell {def.Name} is not available to your nation", false);
        }
        else
        {
            var known = order.Character.Spells
                .Where(s => s.IsKnown && !s.IsLost)
                .Select(s => s.SpellId)
                .ToHashSet();
            var candidates = SpellCatalog.All
                .Where(s => !known.Contains(s.Id) && (!s.IsLost || NationAbilities.CanLearnLostSpell(order.Nation?.Name, s.Id)))
                .ToList();
            if (candidates.Count == 0)
                return MakeResult(order, "All spells already known", false);
            def = candidates[_rng.Next(candidates.Count)];
        }

        if (order.Character.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, $"Already know {def.Name}");

        order.Character.Spells.Add(new Spell
        {
            Id = Guid.NewGuid().ToString(),
            CharacterId = order.Character.Id,
            SpellId = def.Id,
            IsLost = def.IsLost,
            IsKnown = true
        });

        order.Status = "resolved";
        order.Result = $"Researched {def.Name} ({def.College} college)";
        return MakeResult(order, order.Result);
    }

    private object ProcessForgetSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No mage", false);

        Spell? toForget = null;
        if (parameters.TryGetValue("spellId", out var sidEl))
        {
            toForget = order.Character.Spells
                .FirstOrDefault(s => s.SpellId == sidEl.GetInt32() && s.IsKnown && !s.IsLost);
        }
        else
        {
            toForget = order.Character.Spells
                .FirstOrDefault(s => s.IsKnown && !s.IsLost);
        }

        if (toForget == null)
            return MakeResult(order, "No spell to forget");

        toForget.IsLost = true;
        toForget.IsKnown = false;
        var def = SpellCatalog.Get(toForget.SpellId);
        order.Status = "resolved";
        order.Result = $"Forgot {def?.Name ?? "spell"}";
        return MakeResult(order, order.Result);
    }

    private object ProcessPrenticeMagery(Order order, Dictionary<string, JsonElement> parameters)
    {
        var roll = order.Character?.MageSkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 12;

        if (success)
        {
            order.Character!.MageSkill += 2;
            order.Status = "resolved";
            order.Result = $"Apprentice trained (roll {roll}): +2 mage skill";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"Apprentice training failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessCastLoreSpell(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!KnowsSpell(order.Character, SpellType.Lore))
            return SpellUnknown(order, SpellType.Lore);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (!success)
        {
            order.Status = "resolved";
            order.Result = $"Lore spell failed (roll {roll})";
            return MakeResult(order, order.Result);
        }

        // Revelar ejÃ©rcitos enemigos en el hex indicado (o el del lanzador)
        var hex = order.Character.LocationHex;
        if (parameters.TryGetValue("hex", out var hexEl)) hex = hexEl.GetString() ?? hex;

        var seen = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies)
            .Where(a => a.LocationHex == hex)
            .Select(a => $"{a.Name} ({a.Nation.Name})")
            .ToList();

        order.Status = "resolved";
        order.Result = seen.Count > 0
            ? $"Scry reveals at {hex}: {string.Join(", ", seen)}"
            : $"Scry at {hex}: no enemy armies";
        return MakeResult(order, order.Result);
    }

    private object ProcessCastCombatSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        // El hechizo de combate se aplica durante la batalla de la fuerza del personaje
        // (orden 230/235/250/255 o un ataque naval), sumando/restando fuerza segÃºn la
        // tabla de hechizos de combate. AquÃ­ sÃ³lo se registra y resuelve la orden.
        var spell = SpellFromParameters(order.Parameters);
        if (spell == null)
        {
            order.Status = "resolved";
            order.Result = "Combat spell cast (no spell number provided)";
            return MakeResult(order, order.Result);
        }
        order.Status = "resolved";
        order.Result = $"Combat spell {spell} will be cast in battle";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ HELPERS DE HECHIZOS â”€â”€

    private bool KnowsSpell(Character? c, SpellType type)
    {
        if (c == null) return false;
        return c.Spells.Any(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == type);
    }

    private object SpellUnknown(Order order, SpellType type)
    {
        order.Status = "failed";
        order.Result = $"Spell not known: need a {type} spell researched first";
        return MakeResult(order, order.Result, false);
    }

    // â”€â”€ EMISSARY EXTRA â”€â”€

    private object ProcessRecruitDoubleAgent(Order order, Dictionary<string, JsonElement> parameters)
    {
        var roll = order.Character?.EmissarySkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 12;

        order.Status = "resolved";
        order.Result = success
            ? $"Double agent recruited (roll {roll})"
            : $"Recruitment failed (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessBribeCharacter(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "No target specified", false);

        var target = _db.Characters.Find(tgtEl.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);

        var cost = 500;
        if (order.Nation.Gold < cost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {cost}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= cost;
        var roll = order.Character?.EmissarySkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 10;

        order.Status = "resolved";
        order.Result = success
            ? $"Bribe successful (roll {roll}): {target.Name} influenced"
            : $"Bribe failed (roll {roll}): {target.Name} refused";
        return MakeResult(order, order.Result);
    }

    private object ProcessImproveHarbour(Order order, Dictionary<string, JsonElement> parameters)
    {
        // Derivado de la tabla: puerto menos puerto (4000/7500 - 2500/5000).
        const int goldCost = 1500;
        const int timberCost = 2500;
        if (order.Nation.Gold < goldCost || order.Nation.Timber < timberCost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient resources: need {goldCost} gold and {timberCost} timber";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= timberCost;
        order.Status = "resolved";
        order.Result = $"Harbour improved to port for {goldCost} gold and {timberCost} timber";
        return MakeResult(order, order.Result);
    }

    private object ProcessAddHarbour(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at your own population centre", false);
        // Reglamento: puerto 2500 oro + 5000 madera.
        const int goldCost = 2500;
        const int timberCost = 5000;
        if (order.Nation.Gold < goldCost || order.Nation.Timber < timberCost)
            return MakeResult(order, $"Insufficient resources: need {goldCost} gold and {timberCost} timber", false);
        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= timberCost;
        pc.HasHarbour = true;
        order.Status = "resolved";
        order.Result = $"Harbour added to {pc.Name} for {goldCost} gold and {timberCost} timber";
        return MakeResult(order, order.Result);
    }

    private object ProcessImprovePC(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at your own population centre to improve it", false);
        // Reglamento: coste de subida según tamaño (camp 2000 … city 10000).
        int cost = pc.Size.ToLower() switch
        {
            "camp" => 2000,
            "village" => 4000,
            "town" => 6000,
            "major town" => 8000,
            _ => 10000
        };
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        var roll = order.Character?.EmissarySkill + _rng.Next(1, 7) ?? 8;
        if (roll < 10) return MakeResult(order, $"Improvement failed (roll {roll})", false);
        order.Nation.Gold -= cost;
        var idx = Array.IndexOf(SizeOrder, pc.Size.ToLower());
        if (idx >= 0 && idx < SizeOrder.Length - 1)
        {
            pc.Size = SizeOrder[idx + 1];
            order.Status = "resolved";
            order.Result = $"Improved {pc.Name} to {pc.Size} (roll {roll})";
        }
        else
        {
            pc.Production += 100;
            order.Status = "resolved";
            order.Result = $"{pc.Name} production increased (roll {roll})";
        }
        return MakeResult(order, order.Result);
    }

    private object ProcessCreateCamp(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for camp", false);
        // Reglamento: crear campamento 2000 oro.
        const int cost = 2000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        if (_db.PopulationCentres.Any(p => p.LocationHex == hex && p.NationId == order.NationId))
            return MakeResult(order, "Already have a population centre at this hex", false);
        order.Nation.Gold -= cost;
        var camp = new PopulationCentre
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = "Camp",
            Size = "camp",
            LocationHex = hex,
            Loyalty = 50,
            Production = 100,
            Stores = 0
        };
        _db.PopulationCentres.Add(camp);
        order.Status = "resolved";
        order.Result = $"Camp created at {hex} for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessAbandonCamp(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var camp = _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex && p.NationId == order.NationId && p.Size == "camp");
        if (camp == null) return MakeResult(order, "No camp to abandon at this hex", false);
        _db.PopulationCentres.Remove(camp);
        order.Status = "resolved";
        order.Result = $"Camp at {hex} abandoned";
        return MakeResult(order, order.Result);
    }

    private object ProcessReducePC(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = parameters.TryGetValue("hex", out var h) ? h.GetString()
            : (order.Character?.LocationHex ?? order.Army?.LocationHex);
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = PCAtHex(hex);
        if (pc == null) return MakeResult(order, "No population centre at location", false);
        var idx = Array.IndexOf(SizeOrder, pc.Size.ToLower());
        if (idx > 0) pc.Size = SizeOrder[idx - 1];
        pc.Loyalty = Math.Max(0, pc.Loyalty - 20);
        pc.Production = Math.Max(0, pc.Production - 50);
        order.Status = "resolved";
        order.Result = $"Reduced {pc.Name} (now {pc.Size}, loyalty {pc.Loyalty})";
        return MakeResult(order, order.Result);
    }

    private object ProcessSpreadRumours(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Rumours spread";
        return MakeResult(order, order.Result);
    }

    // 520: automática, +1-10 lealtad propia y +1-5 emisario.
    private object ProcessInfluenceOwn(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var hex = order.Character.LocationHex;
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at one of your population centres", false);
        if (order.Character.EmissarySkill <= 0) return MakeResult(order, "Needs emissary skill", false);
        pc.Loyalty = Math.Min(100, pc.Loyalty + _rng.Next(1, 11));
        order.Character.EmissarySkill = Math.Min(100, order.Character.EmissarySkill + _rng.Next(1, 6));
        order.Status = "resolved";
        order.Result = $"Loyalty at {pc.Name} raised to {pc.Loyalty}";
        return MakeResult(order, order.Result);
    }

    // 525: +d6>=12, -5-15 lealtad ajena y +1-10 emisario. Sin toma de control
    // (el reglamento solo da "posibilidad" sin fórmula).
    private object ProcessInfluenceOther(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (order.Character.EmissarySkill <= 0) return MakeResult(order, "Needs emissary skill", false);
        var hex = order.Character.LocationHex;
        var pc = GetPC(game, hex);
        if (pc == null) return MakeResult(order, "No population centre here", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Use 520 on your own centres", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        bool enemyPresent = game.Nations
            .SelectMany(n => n.Armies.Select(a => new { a.LocationHex, NationId = n.Id })
                .Concat(n.Navies.Select(v => new { v.LocationHex, NationId = n.Id })))
            .Any(u => u.LocationHex == hex && u.NationId != order.NationId
                && game.Nations.FirstOrDefault(n => n.Id == u.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == order.NationId)?.Level <= -1);
        if (enemyPresent) return MakeResult(order, "Enemy forces present", false);
        int roll = order.Character.EmissarySkill + _rng.Next(1, 7);
        if (roll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Influence failed at {pc.Name} (roll {roll})";
            return MakeResult(order, order.Result);
        }
        pc.Loyalty = Math.Max(0, pc.Loyalty - _rng.Next(5, 16));
        order.Character.EmissarySkill = Math.Min(100, order.Character.EmissarySkill + _rng.Next(1, 11));
        order.Status = "resolved";
        order.Result = $"Loyalty at {pc.Name} lowered to {pc.Loyalty} (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessUncoverSecrets(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var eff = NationAbilities.UncoverSkill(order.Nation?.Name, order.Character.EmissarySkill);
        var roll = eff + _rng.Next(1, 7);
        var success = roll >= 12;
        order.Status = "resolved";
        order.Result = success
            ? $"Secrets uncovered (roll {roll})"
            : $"No secrets found (roll {roll})";
        return MakeResult(order, order.Result);
    }

    // â”€â”€ SKILL ORDERS (genÃ©rico) â”€â”€

    private object ProcessSkillOrder(Order order, string skillType, Dictionary<string, JsonElement> parameters)
    {
        var skill = (skillType, order.Code) switch
        {
            ("Command", 925) => NationAbilities.ScoutSkill(order.Nation?.Name, 925, order.Character.AgentSkill, order.Character.CommandSkill),
            ("Agent", 910) or ("Agent", 915) => NationAbilities.ScoutSkill(order.Nation?.Name, order.Code, order.Character.AgentSkill, order.Character.CommandSkill),
            ("Command", _) => order.Character.CommandSkill,
            ("Agent", _) => order.Character.AgentSkill,
            ("Emissary", _) => order.Character.EmissarySkill,
            ("Mage", _) => order.Character.MageSkill,
            _ => 8
        };

        var roll = skill + _rng.Next(1, 7);
        var target = 10;
        var success = roll >= target;

        if (success)
        {
            order.Character.ChallengeRank += 1;
            order.Status = "resolved";
            order.Result = $"{skillType} order succeeded (roll {roll})";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"{skillType} order failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }

    private object ProcessGenericOrder(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Order processed (basic resolution)";
        return MakeResult(order, $"Order {order.Code} processed");
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  Ã“RDENES RECONCILIADAS CON EL REGLAMENTO (antes placeholder)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private PopulationCentre? GetPC(Game game, string hexOrId) =>
        game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(p => p.Id == hexOrId || p.LocationHex == hexOrId);

    private Army? GetArmyById(Game game, string id) =>
        game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.Id == id);

    private object ProcessChangeAllegiance(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("allegiance", out var el))
            return MakeResult(order, "Missing allegiance", false);
        order.Nation.Allegiance = el.GetString() ?? order.Nation.Allegiance;
        order.Status = "resolved";
        order.Result = $"Allegiance changed to {order.Nation.Allegiance}";
        return MakeResult(order, order.Result);
    }

    private object ProcessUpgradeRelations(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("nationId", out var nid)) return MakeResult(order, "Missing nationId", false);
        var tid = nid.GetString()!;
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == tid);
        if (rel == null)
        {
            rel = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = order.NationId, TargetNationId = tid, Level = 0 };
            order.Nation.Relations.Add(rel);
        }
        rel.Level = Math.Min(100, rel.Level + 10);
        order.Status = "resolved"; order.Result = $"Relations improved to {rel.Level}";
        return MakeResult(order, order.Result);
    }

    private object ProcessDowngradeRelations(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("nationId", out var nid)) return MakeResult(order, "Missing nationId", false);
        var tid = nid.GetString()!;
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == tid);
        if (rel == null)
        {
            rel = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = order.NationId, TargetNationId = tid, Level = 0 };
            order.Nation.Relations.Add(rel);
        }
        rel.Level = Math.Max(-100, rel.Level - 10);
        order.Status = "resolved"; order.Result = $"Relations worsened to {rel.Level}";
        return MakeResult(order, order.Result);
    }

    private object ProcessIssueChallenge(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No challenger", false);
        if (!p.TryGetValue("targetId", out var tid)) return MakeResult(order, "Missing targetId", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == tid.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);

        // Deben estar en la misma casilla
        if (order.Character.LocationHex != target.LocationHex)
            return MakeResult(order, $"Target not in the same hex (target at {target.LocationHex})", false);

        // Negativa: si el objetivo emitiÃ³ la orden 215 (Rechazar desafÃ­os) este turno, se evita el duelo
        var currentTurn = game.Turns.FirstOrDefault(t => t.Status == "processing")
                          ?? game.Turns.FirstOrDefault(t => t.Status == "orders_open");
        var refused = currentTurn?.Orders.Any(o => o.CharacterId == target.Id && o.Code == 215) ?? false;
        if (refused)
        {
            order.Character.ChallengeRank += 2;
            target.ChallengeRank = Math.Max(0, target.ChallengeRank - 2);
            order.Status = "resolved";
            order.Result = $"Challenge refused by {target.Name}; {order.Character.Name} wins by forfeit (+2 CR), {target.Name} loses 2 CR";
            return MakeResult(order, order.Result);
        }

        var duel = _combat.ResolveDuel(order.Character, target);
        order.Status = "resolved"; order.Result = duel.Message;
        return MakeResult(order, duel.Message);
    }

    private object ProcessRefuseChallenges(Order order, Dictionary<string, JsonElement> p)
    {
        order.Status = "resolved"; order.Result = "All personal challenges refused this turn";
        return MakeResult(order, order.Result);
    }

    // Fase de desafÃ­os personales (reglamento G-1):
    //  - Un solo duelo por personaje por turno (retador y objetivo).
    //  - En la misma casilla; no contra personajes de la propia naciÃ³n.
    //  - Si varios retan al mismo objetivo, lucha el de mayor rango natural.
    //  - Si el objetivo emitiÃ³ 215 (y no tambiÃ©n 210), el desafÃ­o se rechaza (sin duelo).
    private void ResolvePersonalChallenges(Game game, Turn currentTurn, List<object> results)
    {
        var challengeOrders = currentTurn.Orders
            .Where(o => o.Code == 210 && o.Status == "pending" && o.Character != null)
            .ToList();
        if (challengeOrders.Count == 0) return;

        // Personajes que rechazan desafÃ­os este turno (215), salvo que tambiÃ©n hayan emitido 210
        var refused = new HashSet<string>(
            currentTurn.Orders
                .Where(o => o.Code == 215 && o.Status == "pending" && o.Character != null)
                .Select(o => o.CharacterId)
                .Distinct());
        var challengerIds = new HashSet<string>(challengeOrders.Select(o => o.CharacterId));
        refused.ExceptWith(challengerIds); // 215 ignorada si tambiÃ©n emitiÃ³ 210

        var fought = new HashSet<string>();

        foreach (var grp in challengeOrders.GroupBy(o => GetTargetId(o)).Where(g => g.Key != null))
        {
            var target = FindCharacter(game, grp.Key!);
            if (target == null)
            {
                foreach (var o in grp) MarkResolved(o, "Target not found", false, results);
                continue;
            }

            // Retadores ordenados por Challenge Rank descendente; lucha el de mayor CR.
            foreach (var order in grp.OrderByDescending(o => o.Character!.ChallengeRank))
            {
                var challenger = order.Character!;
                if (fought.Contains(challenger.Id) || fought.Contains(target.Id))
                {
                    MarkResolved(order, "Character already fought a personal challenge this turn", false, results);
                    continue;
                }
                if (challenger.NationId == target.NationId)
                {
                    MarkResolved(order, "Cannot challenge a character of your own nation", false, results);
                    continue;
                }
                if (challenger.LocationHex != target.LocationHex)
                {
                    MarkResolved(order, $"Target not in the same hex ({target.LocationHex})", false, results);
                    continue;
                }
                if (target.ArmyId != null && challenger.ArmyId == null)
                {
                    MarkResolved(order, "Cannot challenge an army commander unless you command an army", false, results);
                    continue;
                }
                if (refused.Contains(target.Id))
                {
                    var gain = _rng.Next(1, 16);
                    CombatResolver.IncreaseBestSkill(challenger, gain);
                    challenger.ChallengeRank = _combat.CalculateChallengeRank(challenger);
                    CombatResolver.ApplyCommanderMorale(challenger, gain);
                    CombatResolver.ApplyCommanderMorale(target, -gain);
                    MarkResolved(order, $"Challenge refused by {target.Name}; {challenger.Name} gains +{gain} best skill (no duel)", true, results);
                    fought.Add(challenger.Id); fought.Add(target.Id);
                    continue;
                }

                var duel = _combat.ResolveDuel(challenger, target);
                MarkResolved(order, duel.Message, true, results);
                fought.Add(challenger.Id); fought.Add(target.Id);
                if (challenger.IsDead) HandleCommanderDeath(game, challenger, results);
                if (target.IsDead) HandleCommanderDeath(game, target, results);
                break; // un solo retador por objetivo
            }
        }
    }

    // Reglamento pÃ¡g. 86: si un ejÃ©rcito pierde a su comandante en el duelo y ningÃºn
    // otro personaje con rango de mando (CommandSkill >= 1) puede reemplazarlo, el
    // ejÃ©rcito se desbanda (rout).
    private void HandleCommanderDeath(Game game, Character deadCommander, List<object> results)
    {
        var army = game.Nations.SelectMany(n => n.Armies)
            .FirstOrDefault(a => a.CommanderId == deadCommander.Id);
        if (army == null) return;

        var replacement = army.Characters
            .Where(c => !c.IsDead && c.Id != deadCommander.Id && c.CommandSkill >= 1)
            .OrderByDescending(c => c.CommandSkill)
            .ThenByDescending(c => c.ChallengeRank)
            .FirstOrDefault();

        if (replacement != null)
        {
            army.CommanderId = replacement.Id;
            results.Add(new { type = "commander", armyId = army.Id, message = $"{replacement.Name} assumes command of {army.Name} after {deadCommander.Name}'s death" });
        }
        else
        {
            foreach (var c in army.Characters.Where(c => c.Id != deadCommander.Id).ToList())
                c.ArmyId = null;
            _db.Armies.Remove(army);
            results.Add(new { type = "commander", armyId = army.Id, message = $"{army.Name} routs and is disbanded (commander {deadCommander.Name} slain, no replacement with command rank)" });
        }
    }

    private static string? GetTargetId(Order o)
    {
        try
        {
            using var doc = JsonDocument.Parse(o.Parameters);
            if (doc.RootElement.TryGetProperty("targetId", out var el))
                return el.GetString();
        }
        catch { }
        return null;
    }

    private static Character? FindCharacter(Game game, string id)
        => game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == id);

    private void MarkResolved(Order order, string msg, bool success, List<object> results)
    {
        order.Status = "resolved";
        order.Result = msg;
        results.Add(MakeResult(order, msg, success));
    }

    private object ProcessTransferWarMachines(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "war");
    private object ProcessTransferWeapons(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "weapon");
    private object ProcessTransferArmour(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "armour");
    private object ProcessTransferTroops(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "troops");

    private object TransferBetweenArmies(Order order, Dictionary<string, JsonElement> p, Game game, string kind)
    {
        if (order.Army == null) return MakeResult(order, "No source army", false);
        if (!p.TryGetValue("destArmyId", out var dEl) || !p.TryGetValue("amount", out var aEl))
            return MakeResult(order, "Need destArmyId and amount", false);
        var dest = GetArmyById(game, dEl.GetString()!);
        if (dest == null) return MakeResult(order, "Dest army not found", false);
        var amt = Math.Max(0, aEl.GetInt32());
        switch (kind)
        {
            case "war":
                amt = Math.Min(order.Army.WarMachines, amt); order.Army.WarMachines -= amt; dest.WarMachines += amt; break;
            case "weapon":
                var wAmt = Math.Min(order.Army.HCWeaponRank, amt);
                order.Army.HCWeaponRank -= wAmt; dest.HCWeaponRank += wAmt;
                order.Army.LCWeaponRank -= wAmt; dest.LCWeaponRank += wAmt;
                order.Army.HIWeaponRank -= wAmt; dest.HIWeaponRank += wAmt;
                order.Army.LIWeaponRank -= wAmt; dest.LIWeaponRank += wAmt;
                order.Army.ArcherWeaponRank -= wAmt; dest.ArcherWeaponRank += wAmt;
                order.Army.MAAWeaponRank -= wAmt; dest.MAAWeaponRank += wAmt;
                amt = wAmt;
                break;
            case "armour":
                var aAmt = Math.Min(order.Army.HCArmourRank, amt);
                order.Army.HCArmourRank -= aAmt; dest.HCArmourRank += aAmt;
                order.Army.LCArmourRank -= aAmt; dest.LCArmourRank += aAmt;
                order.Army.HIArmourRank -= aAmt; dest.HIArmourRank += aAmt;
                order.Army.LIArmourRank -= aAmt; dest.LIArmourRank += aAmt;
                order.Army.ArcherArmourRank -= aAmt; dest.ArcherArmourRank += aAmt;
                order.Army.MAAArmourRank -= aAmt; dest.MAAArmourRank += aAmt;
                amt = aAmt;
                break;
            case "troops":
                var hc = Math.Min(order.Army.HeavyCavalry, amt); order.Army.HeavyCavalry -= hc; dest.HeavyCavalry += hc;
                var rest = amt - hc;
                var li = Math.Min(order.Army.LightInfantry, rest); order.Army.LightInfantry -= li; dest.LightInfantry += li;
                amt = hc + li;
                break;
        }
        order.Status = "resolved"; order.Result = $"Transferred {amt} {kind} to {dest.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransferShips(Order order, Dictionary<string, JsonElement> p)
    {
        order.Status = "resolved"; order.Result = "Ships transferred (basic resolution)";
        return MakeResult(order, order.Result);
    }

    private object ProcessRemoveHarbour(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.HasHarbour = false;
        order.Status = "resolved"; order.Result = $"Harbour removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessRemovePort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.HasPort = false;
        order.Status = "resolved"; order.Result = $"Port removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessDestroyStores(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : pc.Stores;
        pc.Stores = Math.Max(0, pc.Stores - amt);
        order.Status = "resolved"; order.Result = $"Destroyed {amt} stores at {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessRemoveFort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.Fortification = null;
        order.Status = "resolved"; order.Result = $"Fortifications removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessFortifyPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only fortify your own population centres", false);
        if ((pc.Fortification ?? "").Contains("itadel", StringComparison.OrdinalIgnoreCase))
            return MakeResult(order, $"{pc.Name} already has citadel-class fortifications", false);
        if (_fortifiedThisTurn.Contains(pc.Id))
            return MakeResult(order, $"{pc.Name} was already fortified this turn", false);
        if (order.Character == null) return MakeResult(order, "No character", false);
        int roll = order.Character.CommandSkill + _rng.Next(1, 7);
        if (roll < 12)
            return MakeResult(order, $"Failed to fortify {pc.Name} (roll {roll})", false);
        // Escalera oficial Tower-Fort-Castle-Keep-Citadel: solo sube un nivel
        // por turno (el parámetro 1-5 equivale al tipo; por defecto el siguiente).
        var ladder = new[] { "Tower", "Fort", "Castle", "Keep", "Citadel" };
        int cur = FortLadderIndex(pc.Fortification);
        int want = cur + 1;
        if (p.TryGetValue("level", out var lv)) want = Math.Clamp(lv.GetInt32(), 1, 5) - 1;
        if (want != cur + 1 || want < 0 || want > 4)
            return MakeResult(order, $"Must build exactly one level (next: {(cur + 1 <= 4 ? ladder[cur + 1] : "none")})", false);
        var fort = ladder[want];
        // Costes D-2. Madera de stores del PC.
        int timber = NationAbilities.FortTimberCost(order.Nation?.Name, fort ?? "palisade");
        int gold = NationAbilities.FortGoldCost(fort ?? "palisade");
        if (pc.Stores < timber || order.Nation.Gold < gold)
            return MakeResult(order, $"Insufficient stores: need {timber} timber and {gold} gold", false);
        pc.Stores -= timber;
        order.Nation.Gold -= gold;
        pc.Fortification = fort;
        _fortifiedThisTurn.Add(pc.Id);
        order.Character.CommandSkill = Math.Min(100, order.Character.CommandSkill + _rng.Next(1, 6));
        order.Status = "resolved"; order.Result = $"Fortified {pc.Name} to {pc.Fortification} for {timber} timber and {gold} gold (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessThreatenPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Cannot threaten your own centre (use 520)", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        // Solo naciones desagradas/odiadas (nivel <= -1 hacia ellas).
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == pc.NationId);
        if (rel == null || rel.Level > -1)
            return MakeResult(order, "Can only threaten disliked or hated nations", false);
        // Solo comandante de ejército/armada.
        bool commands = order.Character?.ArmyId != null || order.Army != null
            || game.Nations.SelectMany(n => n.Navies).Any(v => v.CommanderId == order.Character?.Id);
        if (!commands) return MakeResult(order, "Only an army or navy commander can threaten", false);
        // Sin enemigos presentes (los que nos consideran enemigos).
        bool enemyPresent = game.Nations
            .SelectMany(n => n.Armies.Select(a => new { a.LocationHex, NationId = n.Id })
                .Concat(n.Navies.Select(v => new { v.LocationHex, NationId = n.Id })))
            .Any(u => u.LocationHex == pc.LocationHex && u.NationId != order.NationId
                && game.Nations.FirstOrDefault(n => n.Id == u.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == order.NationId)?.Level <= -1);
        if (enemyPresent) return MakeResult(order, "Enemy forces present", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : 10;
        pc.Loyalty = Math.Max(0, pc.Loyalty - amt);
        order.Status = "resolved"; order.Result = $"Threatened {pc.Name}, loyalty down to {pc.Loyalty}";
        return MakeResult(order, order.Result);
    }

    private object ProcessTransferOwnership(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.NationId = order.NationId;
        order.Status = "resolved"; order.Result = $"Ownership of {pc.Name} transferred to {order.Nation.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessRelocateCapital(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        // Reglamento: 25000 oro.
        const int cost = 25000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        order.Nation.Gold -= cost;
        foreach (var c in order.Nation.PopulationCentres) c.IsCapital = false;
        pc.IsCapital = true;
        order.Status = "resolved"; order.Result = $"Capital relocated to {pc.Name} for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private PopulationCentre? ResolvePC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (p.TryGetValue("pcId", out var idEl)) return GetPC(game, idEl.GetString()!);
        if (p.TryGetValue("hex", out var hEl)) return GetPC(game, hEl.GetString()!);
        // Default: PC en la ubicaciÃ³n del ejÃ©rcito/personaje
        var hex = order.Army?.LocationHex ?? order.Character?.LocationHex;
        return hex != null ? GetPC(game, hex) : null;
    }

    private object ProcessNameCharacter(Order order, Dictionary<string, JsonElement> p, string type)
    {
        if (!p.TryGetValue("name", out var nEl)) return MakeResult(order, "Missing name", false);
        // Reglamento: multi (725) 10000 oro; resto 5000.
        int cost = order.Code == 725 ? 10000 : 5000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        var capital = order.Nation.PopulationCentres.FirstOrDefault(x => x.IsCapital)
                      ?? order.Nation.PopulationCentres.FirstOrDefault();
        // Rango inicial 40 si la nación tiene NAME_<TIPO>_40; sigilo/desafío
        // extra (1d6) si tiene NEWCHAR_STEALTH / NEWCHAR_CHALLENGE.
        var startSkill = NationAbilities.NameCharacterSkill(order.Nation?.Name, type);
        var ch = new Character
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = nEl.GetString()!,
            Type = type,
            LocationHex = capital?.LocationHex ?? "0,0",
            MaxHealth = 100,
            Health = 100,
            Stealth = NationAbilities.HasForNation(order.Nation?.Name, "NEWCHAR_STEALTH") ? _rng.Next(1, 7) : 0,
            ChallengeRank = NationAbilities.HasForNation(order.Nation?.Name, "NEWCHAR_CHALLENGE") ? _rng.Next(1, 7) : 0
        };
        if (type == "mage") ch.MageSkill = startSkill;
        if (type == "agent") ch.AgentSkill = startSkill;
        if (type == "emissary") ch.EmissarySkill = startSkill;
        if (type == "commander") ch.CommandSkill = startSkill;
        _db.Characters.Add(ch);
        order.Nation.Gold -= cost;
        order.Status = "resolved"; order.Result = $"Named new {type}: {ch.Name} for {cost} gold";
        return MakeResult(order, order.Result);
    }

    private object ProcessHireArmy(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("name", out var nEl)) return MakeResult(order, "Missing name", false);
        // Reglamento: 5000 de oro fijos; gratis con HIRE_FREE.
        var cost = NationAbilities.HireArmyCost(order.Nation?.Name);
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        var capital = order.Nation.PopulationCentres.FirstOrDefault(x => x.IsCapital)
                      ?? order.Nation.PopulationCentres.FirstOrDefault();
        order.Nation.Gold -= cost;
        var army = new Army
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = nEl.GetString()!,
            LocationHex = capital?.LocationHex ?? "0,0",
            Morale = NationAbilities.HireMorale(order.Nation?.Name),
            Training = 10
        };
        _db.Armies.Add(army);
        order.Status = "resolved"; order.Result = $"Hired army: {army.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessDisbandArmy(Order order, Dictionary<string, JsonElement> p)
    {
        var army = order.Army;
        if (army == null && p.TryGetValue("armyId", out var aEl))
            army = _db.Armies.Find(aEl.GetString());
        if (army == null) return MakeResult(order, "No army to disband", false);
        _db.Armies.Remove(army);
        order.Status = "resolved"; order.Result = $"Disbanded army: {army.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessScoutArmy(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character?.LocationHex ?? order.Army?.LocationHex ?? "");
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 905, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Scout at {hex}: nothing found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies).Where(a => a.LocationHex == hex)
            .Select(a => $"{a.Name} ({a.Nation.Name}) HC:{a.HeavyCavalry} HI:{a.HeavyInfantry}").ToList();
        order.Status = "resolved";
        order.Result = seen.Count > 0 ? $"Scout at {hex}: {string.Join(", ", seen)}" : $"Scout at {hex}: nothing";
        return MakeResult(order, order.Result);
    }

    private object ProcessScoutPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character?.LocationHex ?? "");
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 920, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 10)
        {
            order.Status = "resolved";
            order.Result = $"Scout PC at {hex}: nothing found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.PopulationCentres).Where(pc => pc.LocationHex == hex)
            .Select(pc => $"{pc.Name} ({pc.Nation?.Name ?? "?"}) L:{pc.Loyalty}").ToList();
        order.Status = "resolved";
        order.Result = seen.Count > 0 ? $"Scout PC at {hex}: {string.Join(", ", seen)}" : $"Scout PC at {hex}: nothing";
        return MakeResult(order, order.Result);
    }

    private object ProcessScoutCharacters(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character?.LocationHex ?? "");
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 930, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Scout chars at {hex}: none found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Characters).Where(c => c.LocationHex == hex && !c.IsDead)
            .Select(c => $"{c.Name} ({c.Nation.Name})").ToList();
        order.Status = "resolved";
        order.Result = seen.Count > 0 ? $"Scout chars at {hex}: {string.Join(", ", seen)}" : $"Scout chars at {hex}: none";
        return MakeResult(order, order.Result);
    }

    private object ProcessGuardLocation(Order order, Dictionary<string, JsonElement> p)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character.LocationHex);
        _db.Guards.Add(new Guard { Id = Guid.NewGuid().ToString(), CharacterId = order.Character.Id, TargetId = hex });
        order.Status = "resolved"; order.Result = $"Guarding location {hex}";
        return MakeResult(order, order.Result);
    }

    private object ProcessGuardCharacter(Order order, Dictionary<string, JsonElement> p)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (!p.TryGetValue("targetId", out var t)) return MakeResult(order, "Missing targetId", false);
        _db.Guards.Add(new Guard { Id = Guid.NewGuid().ToString(), CharacterId = order.Character.Id, TargetId = t.GetString()! });
        order.Status = "resolved"; order.Result = "Guarding character";
        return MakeResult(order, order.Result);
    }

    private object ProcessAssassinate(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No assassin", false);
        if (!p.TryGetValue("targetId", out var t)) return MakeResult(order, "Missing targetId", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == t.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);
        var effA = NationAbilities.AssassinSkill(order.Nation?.Name, order.Character.AgentSkill);
        var roll = effA + _rng.Next(1, 7);
        if (roll >= 12)
        {
            target.Health = 0; target.IsDead = true;
            order.Status = "resolved"; order.Result = $"Assassination succeeded: {target.Name} is dead (roll {roll})";
        }
        else
        {
            var dmg = _rng.Next(10, 40);
            target.Health = Math.Max(0, target.Health - dmg);
            order.Status = "resolved"; order.Result = $"Assassination failed (roll {roll}): {target.Name} took {dmg} damage";
        }
        return MakeResult(order, order.Result);
    }

    private object ProcessSabotageFort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.Fortification = pc.Fortification switch { "fortress" => "castle", "castle" => "walls", "walls" => "palisade", "palisade" => null, _ => null };
        order.Status = "resolved"; order.Result = $"Sabotaged fortifications at {pc.Name} (now {pc.Fortification})";
        return MakeResult(order, order.Result);
    }

    private object ProcessSabotagePort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.HasPort = false; pc.HasHarbour = false;
        order.Status = "resolved"; order.Result = $"Sabotaged harbour/port at {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessSabotageProduction(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : pc.Stores;
        pc.Stores = Math.Max(0, pc.Stores - amt);
        pc.Production = Math.Max(0, pc.Production - 50);
        order.Status = "resolved"; order.Result = $"Sabotaged production at {pc.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessStealArtifact(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("artifactId", out var aId)) return MakeResult(order, "Missing artifactId", false);
        var art = _db.Artifacts.Find(aId.GetString());
        if (art == null) return MakeResult(order, "Artifact not found", false);
        art.NationId = order.NationId;
        art.HeldByCharacterId = order.Character?.Id;
        art.IsAtCapital = order.Character == null;
        order.Status = "resolved"; order.Result = $"Stole artifact {art.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessStealGold(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (!p.TryGetValue("nationId", out var nid) || !p.TryGetValue("amount", out var aEl))
            return MakeResult(order, "Missing nationId/amount", false);
        var victim = game.Nations.FirstOrDefault(n => n.Id == nid.GetString());
        if (victim == null) return MakeResult(order, "Victim nation not found", false);
        var amt = Math.Min(victim.Gold, Math.Max(0, aEl.GetInt32()));
        victim.Gold -= amt; order.Nation.Gold += amt;
        order.Status = "resolved"; order.Result = $"Stole {amt} gold from {victim.Name}";
        return MakeResult(order, order.Result);
    }

    private object ProcessDestroyBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: personaje con mando; si va suelto exige PC propia en el hex
        // (el comandante de ejercito/armada no la necesita). Exito por rango de mando.
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasBridge) return MakeResult(order, $"No bridge at {hex}", false);
        bool commands = ch.ArmyId != null || _db.Navies.Any(n => n.CommanderId == ch.Id);
        if (!commands && !_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == ch.NationId))
            return MakeResult(order, "Destroying a bridge alone requires one of your population centres in the hex", false);
        int roll = ch.CommandSkill + _rng.Next(1, 7);
        if (roll < 12)
            return MakeResult(order, $"Failed to destroy the bridge at {hex} (roll {roll})", false);
        tile.HasBridge = false;
        order.Status = "resolved"; order.Result = $"Bridge at {hex} destroyed (roll {roll})";
        return MakeResult(order, order.Result);
    }
    private object ProcessBuildBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: personaje con mando (+PC propia en el hex salvo comandante);
        // menor: 5000 madera + 2500 oro; mayor: 10000 madera + 5000 oro;
        // el mayor exige camino (une las dos mitades); sin vado/puente previo.
        // Exito por rango de mando (madera/oro solo se cobran si sale).
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasMajorRiver && !tile.HasMinorRiver)
            return MakeResult(order, $"No river at {hex}", false);
        if (tile.HasBridge || tile.HasFord)
            return MakeResult(order, $"A ford or bridge already exists at {hex}", false);
        bool major = tile.HasMajorRiver;
        if (major && !tile.HasRoad)
            return MakeResult(order, $"A bridge over a major river at {hex} requires a road", false);
        int timber = major ? 10000 : 5000;
        int gold = major ? 5000 : 2500;
        if (order.Nation.Timber < timber || order.Nation.Gold < gold)
            return MakeResult(order, $"Insufficient stores: need {timber} timber and {gold} gold", false);
        bool commands = ch.ArmyId != null || _db.Navies.Any(n => n.CommanderId == ch.Id);
        if (!commands && !_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == ch.NationId))
            return MakeResult(order, "Building a bridge alone requires one of your population centres in the hex", false);
        int roll = ch.CommandSkill + _rng.Next(1, 7);
        int need = major ? 18 : 12;
        if (roll < need)
            return MakeResult(order, $"Failed to build the bridge at {hex} (roll {roll} vs {need})", false);
        order.Nation.Timber -= timber;
        order.Nation.Gold -= gold;
        tile.HasBridge = true;
        order.Status = "resolved"; order.Result = $"Bridge built at {hex} for {timber} timber and {gold} gold (roll {roll})";
        return MakeResult(order, order.Result);
    }
    private object ProcessSabotageBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: agente; se opone el mejor guardian del hex (orden 605),
        // sin guardian vale dificultad fija. Exito por rango de agente.
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasBridge) return MakeResult(order, $"No bridge at {hex}", false);
        var guardIds = _db.Guards.Where(g => g.TargetId == hex).Select(g => g.CharacterId).ToList();
        var guards = _db.Characters.Where(c => guardIds.Contains(c.Id) && !c.IsDead).ToList();
        int defense = guards.Count > 0 ? guards.Max(g => g.AgentSkill) + _rng.Next(1, 7) : 14;
        int roll = ch.AgentSkill + _rng.Next(1, 7);
        if (roll < defense)
            return MakeResult(order, $"Bridge sabotage at {hex} thwarted (roll {roll} vs {defense})", false);
        tile.HasBridge = false;
        order.Status = "resolved"; order.Result = $"Bridge at {hex} sabotaged (roll {roll} vs {defense})";
        return MakeResult(order, order.Result);
    }
    private object ProcessPostCamp(Order order, Dictionary<string, JsonElement> p)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for camp", false);
        // Reglamento: asentar campamento 4000 oro.
        const int cost = 4000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        if (_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == order.NationId))
            return MakeResult(order, "Already have a population centre at this hex", false);
        order.Nation.Gold -= cost;
        var camp = new PopulationCentre
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = "Camp",
            Size = "camp",
            LocationHex = hex,
            Loyalty = 50,
            Production = 100,
            Stores = 0
        };
        _db.PopulationCentres.Add(camp);
        order.Status = "resolved";
        order.Result = $"Camp posted at {hex} for {cost} gold";
        return MakeResult(order, order.Result);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  UTILIDADES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    private static void ReduceTroops(Army army, int casualties)
    {
        var total = army.HeavyCavalry + army.LightCavalry
                  + army.HeavyInfantry + army.LightInfantry
                  + army.Archers + army.MenAtArms;

        if (total == 0) return;
        var ratio = (double)(total - casualties) / total;
        army.HeavyCavalry = (int)(army.HeavyCavalry * ratio);
        army.LightCavalry = (int)(army.LightCavalry * ratio);
        army.HeavyInfantry = (int)(army.HeavyInfantry * ratio);
        army.LightInfantry = (int)(army.LightInfantry * ratio);
        army.Archers = (int)(army.Archers * ratio);
        army.MenAtArms = (int)(army.MenAtArms * ratio);
    }

    private static int GetSeasonBonus(int turnNumber)
    {
        var season = ((turnNumber - 1) % 4) switch
        {
            0 => "spring",
            1 => "summer",
            2 => "autumn",
            _ => "winter"
        };
        return season switch
        {
            "spring" => 20,
            "summer" => 50,
            "autumn" => 10,
            "winter" => -30,
            _ => 0
        };
    }

    private static string GetCurrentSeason(int turnNumber)
    {
        return ((turnNumber - 1) % 4) switch
        {
            0 => "spring",
            1 => "summer",
            2 => "autumn",
            _ => "winter"
        };
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
