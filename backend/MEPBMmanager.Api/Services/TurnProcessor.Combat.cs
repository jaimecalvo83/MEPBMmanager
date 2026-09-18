using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Battles, sieges, assaults and battle aftermath.</summary>
public sealed partial class TurnProcessor
{

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

        if (battleResult.Winner == "attacker") AwardBattleXp(order.Army, game);
        else if (battleResult.Winner == "defender") AwardBattleXp(enemyArmy, game);
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

        if (battleResult.Winner == "attacker") AwardBattleXp(order.Army, game);
        else if (battleResult.Winner == "defender") AwardBattleXp(enemyArmy, game);
        CleanupDisbanded(order.Army, game);
        CleanupDisbanded(enemyArmy, game);

        order.Status = "resolved";
        order.Result = $"Nation attack: {battleResult.Message}";
        return MakeResult(order, order.Result);
    }


    private static int TotalTroops(Army a)
        => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms;


    // XP de batalla: +2 training al ejército vencedor y +1 mando a su comandante.
    private void AwardBattleXp(Army army, Game game)
    {
        army.Training = Math.Min(100, army.Training + 2);
        if (string.IsNullOrEmpty(army.CommanderId)) return;
        var boss = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == army.CommanderId);
        if (boss != null && !boss.IsDead)
            boss.CommandSkill = Math.Min(100, boss.CommandSkill + 1);
    }


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

        if (result.Winner == "attacker") AwardBattleXp(order.Army, game);
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

        if (result.Winner == "attacker") AwardBattleXp(order.Army, game);
        CleanupDisbanded(order.Army, game);
        order.Status = "resolved";
        order.Result = result.Message;
        return MakeResult(order, $"Destroy: {result.Message} (lost {result.AttackerCasualties} troops)");
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
}
