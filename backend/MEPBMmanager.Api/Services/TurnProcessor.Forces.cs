using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Companies and armies: lifecycle and command.</summary>
public sealed partial class TurnProcessor
{

    // â”€â”€ COMPANIES â”€â”€

    private object ProcessCreateCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character != null && (order.Character.ArmyId != null || order.Character.CompanyId != null
            || _db.Navies.Any(v => v.CommanderId == order.Character!.Id)))
            return MakeResult(order, "Character already commands a force", false);
        if (order.Character != null && !IsLandHex(order.GameId, order.Character.LocationHex))
            return MakeResult(order, "Must be on land", false);
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


    private object ProcessJoinCompany(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!parameters.TryGetValue("commanderId", out var cidEl))
            return MakeResult(order, "Missing commanderId: join through the company commander", false);

        var commander = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == cidEl.GetString());
        if (commander == null || commander.CompanyId == null)
            return MakeResult(order, "Commander has no company", false);
        var company = _db.Companies.Find(commander.CompanyId);
        if (company == null)
            return MakeResult(order, "Company not found", false);
        var members = _db.Characters.Count(c => c.CompanyId == company.Id && !c.IsDead);
        if (members >= 9)
            return MakeResult(order, $"Company '{company.Name}' is full (9 members)", false);
        if (company.NationId != order.NationId && !SameOrFriendly(game, order.NationId, company.NationId))
            return MakeResult(order, "Company must be of the same or a friendly nation", false);

        if (order.Character != null)
            order.Character.CompanyId = company.Id;

        order.Status = "resolved";
        order.Result = $"Joined company '{company.Name}'";
        return MakeResult(order, order.Result);
    }


    private object ProcessTransferCommand(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tEl))
            return MakeResult(order, "Missing targetId", false);
        var target = _db.Characters.Find(tEl.GetString());
        if (target != null)
        {
            if (target.NationId != order.NationId)
                return MakeResult(order, "New commander must be of the same nation", false);
            if (target.CommandSkill <= 0)
                return MakeResult(order, "New commander needs command skill", false);
        }
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


    private object ProcessLeaveCompany(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character?.CompanyId == null)
            return MakeResult(order, "Not in a company", false);

        order.Character.CompanyId = null;
        order.Status = "resolved";
        order.Result = "Left company";
        return MakeResult(order, order.Result);
    }


    private object ProcessSplitArmy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Army == null) return MakeResult(order, "No army to split", false);
        if (!parameters.TryGetValue("commanderId", out var cmdEl))
            return MakeResult(order, "Missing commanderId for the new army", false);
        var newBoss = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == cmdEl.GetString());
        if (newBoss == null || newBoss.IsDead) return MakeResult(order, "New commander not found", false);
        if (newBoss.NationId != order.NationId) return MakeResult(order, "New commander must be of the same nation", false);
        if (newBoss.CommandSkill <= 0) return MakeResult(order, "New commander needs command skill", false);
        if (newBoss.ArmyId != null || newBoss.CompanyId != null
            || game.Nations.SelectMany(n => n.Navies).Any(v => v.CommanderId == newBoss.Id))
            return MakeResult(order, $"{newBoss.Name} already commands a force", false);
        if (newBoss.LocationHex != order.Army.LocationHex)
            return MakeResult(order, "New commander must be at the same hex", false);

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
            Morale = order.Army.Morale,
            Training = order.Army.Training,
            HCTraining = order.Army.HCTraining,
            LCTraining = order.Army.LCTraining,
            HITraining = order.Army.HITraining,
            LITraining = order.Army.LITraining,
            ArcherTraining = order.Army.ArcherTraining,
            MAATraining = order.Army.MAATraining,
            CommanderId = newBoss.Id
        };

        order.Army.HeavyCavalry -= newArmy.HeavyCavalry;
        order.Army.LightCavalry -= newArmy.LightCavalry;
        order.Army.HeavyInfantry -= newArmy.HeavyInfantry;
        order.Army.LightInfantry -= newArmy.LightInfantry;
        order.Army.Archers -= newArmy.Archers;
        order.Army.MenAtArms -= newArmy.MenAtArms;
        newBoss.ArmyId = newArmy.Id;

        _db.Armies.Add(newArmy);
        order.Status = "resolved";
        order.Result = $"Army split: {newArmy.Name} created under {newBoss.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessJoinArmy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character != null && (order.Character.ArmyId != null || order.Character.CompanyId != null
            || game.Nations.SelectMany(n => n.Navies).Any(v => v.CommanderId == order.Character!.Id)))
            return MakeResult(order, "Character already commands a force", false);
        if (!parameters.TryGetValue("commanderId", out var cmdEl))
            return MakeResult(order, "Missing commanderId of the force to join", false);

        var boss = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == cmdEl.GetString());
        if (boss == null || boss.IsDead) return MakeResult(order, "Commander not found", false);
        if (boss.NationId != order.NationId) return MakeResult(order, "Can only join a force of your own nation", false);
        var army = game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
        var navy = army == null ? game.Nations.SelectMany(n => n.Navies).FirstOrDefault(v => v.CommanderId == boss.Id) : null;
        if (army == null && navy == null) return MakeResult(order, $"{boss.Name} commands no force", false);
        var hex = army?.LocationHex ?? navy!.LocationHex;
        if (hex != order.Character?.LocationHex)
            return MakeResult(order, "Force to join must be at the same hex", false);

        if (order.Character != null)
        {
            order.Character.ArmyId = army?.Id;
            order.Character.LocationHex = hex;
        }

        order.Status = "resolved";
        order.Result = $"Joined {(army != null ? "army" : "navy")} of {boss.Name} at {hex}";
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


    private object ProcessTransferShips(Order order, Dictionary<string, JsonElement> p)
    {
        order.Status = "resolved"; order.Result = "Ships transferred (basic resolution)";
        return MakeResult(order, order.Result);
    }


    private object ProcessHireArmy(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("name", out var nEl)) return MakeResult(order, "Missing name", false);
        if (order.Character != null && (order.Character.ArmyId != null || order.Character.CompanyId != null
            || _db.Navies.Any(v => v.CommanderId == order.Character!.Id)))
            return MakeResult(order, "Character already commands a force", false);
        var hex = order.Character?.LocationHex;
        var pc = hex == null ? null : OwnedPCAt(hex, order.NationId);
        if (pc == null || pc.IsSieged)
            return MakeResult(order, "Must be at one of your non-sieged population centres", false);
        var troopType = "MenAtArms";
        if (p.TryGetValue("troopType", out var ttEl)) troopType = ttEl.GetString() ?? troopType;
        if (TroopShortKeys.TryGetValue(troopType, out var fullType)) troopType = fullType;
        if (!UpgradeableTroopTypes.Contains(troopType, StringComparer.OrdinalIgnoreCase))
            return MakeResult(order, "Troop type must be hc, lc, hi, li, ar or ma", false);
        troopType = UpgradeableTroopTypes.First(t => t.Equals(troopType, StringComparison.OrdinalIgnoreCase));
        var troops = p.TryGetValue("troops", out var trEl) ? Math.Max(0, trEl.GetInt32()) : 100;
        if (troops <= 0) return MakeResult(order, "Troop count must be positive", false);
        var weapons = "bronze";
        if (p.TryGetValue("weapons", out var wEl)) weapons = wEl.GetString() ?? weapons;
        var armour = "leather";
        if (p.TryGetValue("armour", out var aEl)) armour = aEl.GetString() ?? armour;
        if (!MaterialRank.TryGetValue(weapons, out var wRank) || (weapons != "bronze" && weapons != "steel" && weapons != "mithril"))
            return MakeResult(order, "Weapon material must be bronze, steel or mithril", false);
        if (!MaterialRank.TryGetValue(armour, out var aRank))
            return MakeResult(order, "Armour material must be leather, bronze, steel or mithril", false);
        var food = p.TryGetValue("food", out var fEl) ? Math.Max(0, fEl.GetInt32()) : 0;
        // Reglamento: 5000 de oro fijos; gratis con HIRE_FREE.
        var cost = NationAbilities.HireArmyCost(order.Nation?.Name);
        var matUnits = Math.Max(1, (troops + 99) / 100);
        var needsMount = troopType is "HeavyCavalry" or "LightCavalry";
        if (order.Nation.Gold < cost || order.Nation.Food < food
            || (needsMount && order.Nation.Mounts < troops)
            || MaterialStock(order.Nation, weapons) < matUnits || MaterialStock(order.Nation, armour) < matUnits)
            return MakeResult(order, $"Insufficient resources: need {cost} gold, {food} food"
                + (needsMount ? $", {troops} mounts" : "")
                + $", {matUnits} {weapons} and {matUnits} {armour}", false);
        order.Nation.Gold -= cost;
        order.Nation.Food = Math.Max(0, order.Nation.Food - food);
        if (needsMount) order.Nation.Mounts -= troops;
        ConsumeMaterial(order.Nation, weapons, matUnits);
        ConsumeMaterial(order.Nation, armour, matUnits);
        var army = new Army
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = nEl.GetString()!,
            LocationHex = pc.LocationHex,
            Morale = NationAbilities.HireMorale(order.Nation?.Name),
            Training = 10,
            Food = food
        };
        SetTroopCount(army, troopType, troops);
        SetTroopWeaponRank(army, troopType, wRank);
        SetTroopArmourRank(army, troopType, aRank);
        SetTroopTraining(army, troopType, 10);
        _db.Armies.Add(army);
        order.Status = "resolved"; order.Result = $"Hired army: {army.Name} ({troops} {troopType}) for {cost} gold";
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
}
