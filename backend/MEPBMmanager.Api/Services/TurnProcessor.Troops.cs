using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Recruitment, training, equipment and war machines.</summary>
public sealed partial class TurnProcessor
{

    private Dictionary<string, int> _recruitsUsed = new();


    // Rango de arma/armadura por material (370/375). Un solo material por orden.
    private static readonly Dictionary<string, int> MaterialRank = new(StringComparer.OrdinalIgnoreCase)
    {
        { "leather", 10 }, { "bronze", 30 }, { "steel", 60 }, { "mithril", 100 }
    };


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

        var weaponMat = "bronze";
        if (parameters.TryGetValue("weapons", out var wEl)) weaponMat = wEl.GetString() ?? weaponMat;
        var armourMat = "leather";
        if (parameters.TryGetValue("armour", out var arEl)) armourMat = arEl.GetString() ?? armourMat;
        if (!MaterialRank.TryGetValue(weaponMat, out var weaponRank))
            return MakeResult(order, "Weapon material must be leather, bronze, steel or mithril", false);
        if (!MaterialRank.TryGetValue(armourMat, out var armourRank))
            return MakeResult(order, "Armour material must be leather, bronze, steel or mithril", false);

        var amount = requested;
        if (amount > available) amount = available;
        if (order.Nation.Gold < amount * costPerUnit)
            amount = order.Nation.Gold / costPerUnit;
        if (needsMount && order.Nation.Mounts < amount)
            amount = order.Nation.Mounts;
        var matUnits = Math.Max(1, (amount + 99) / 100);
        if (MaterialStock(order.Nation, weaponMat) < matUnits || MaterialStock(order.Nation, armourMat) < matUnits)
            amount = 0;
        if (amount <= 0)
            return MakeResult(order, $"Cannot recruit {troopType}: insufficient gold, mounts or materials", false);

        var totalCost = amount * costPerUnit;
        order.Nation.Gold -= totalCost;
        if (needsMount) order.Nation.Mounts -= amount;
        ConsumeMaterial(order.Nation, weaponMat, matUnits);
        ConsumeMaterial(order.Nation, armourMat, matUnits);

        var existing = CountTroops(order.Army);
        var typeCount = TroopCount(order.Army, troopType);
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
        SetTroopWeaponRank(order.Army, troopType, Math.Max(TroopWeaponRank(order.Army, troopType), weaponRank));
        SetTroopArmourRank(order.Army, troopType, Math.Max(TroopArmourRank(order.Army, troopType), armourRank));
        // Training medio de la agrupación (tipo): los nuevos promedian con los que ya hay.
        var typeBase = Math.Max(TroopTraining(order.Army, troopType), 10);
        SetTroopTraining(order.Army, troopType, typeCount == 0
            ? recruitTraining
            : (typeCount * typeBase + amount * recruitTraining) / (typeCount + amount));
        var newTotal = existing + amount;
        order.Army.Training = existing == 0
            ? recruitTraining
            : (existing * baseTraining + amount * recruitTraining) / newTotal;

        _recruitsUsed[pc.Id] = used + amount;

        order.Status = "resolved";
        var note = amount < requested ? " (limited by availability/gold/mounts/materials)" : "";
        order.Result = $"Recruited {amount} {troopType} at {pc.Name} for {totalCost} gold ({weaponMat}/{armourMat} gear, {matUnits} each){note}";
        return MakeResult(order, order.Result);
    }


    // 370/375: un solo material por orden; sube el rango de los tipos asignados
    // (por defecto todos los presentes) hasta el rango del material, con tope
    // de tropas por la cantidad indicada. Consume oro + material en stock.
    private static readonly string[] UpgradeableTroopTypes =
        { "HeavyCavalry", "LightCavalry", "HeavyInfantry", "LightInfantry", "Archers", "MenAtArms" };


    private static int TroopCount(Army army, string type) => type switch
    {
        "HeavyCavalry" => army.HeavyCavalry,
        "LightCavalry" => army.LightCavalry,
        "HeavyInfantry" => army.HeavyInfantry,
        "LightInfantry" => army.LightInfantry,
        "Archers" => army.Archers,
        "MenAtArms" => army.MenAtArms,
        _ => 0
    };


    private static int TroopTraining(Army army, string type) => type switch
    {
        "HeavyCavalry" => army.HCTraining,
        "LightCavalry" => army.LCTraining,
        "HeavyInfantry" => army.HITraining,
        "LightInfantry" => army.LITraining,
        "Archers" => army.ArcherTraining,
        "MenAtArms" => army.MAATraining,
        _ => 10
    };


    private static void SetTroopTraining(Army army, string type, int value)
    {
        switch (type)
        {
            case "HeavyCavalry": army.HCTraining = value; break;
            case "LightCavalry": army.LCTraining = value; break;
            case "HeavyInfantry": army.HITraining = value; break;
            case "LightInfantry": army.LITraining = value; break;
            case "Archers": army.ArcherTraining = value; break;
            case "MenAtArms": army.MAATraining = value; break;
        }
    }


    private static int TroopWeaponRank(Army army, string type) => type switch
    {
        "HeavyCavalry" => army.HCWeaponRank,
        "LightCavalry" => army.LCWeaponRank,
        "HeavyInfantry" => army.HIWeaponRank,
        "LightInfantry" => army.LIWeaponRank,
        "Archers" => army.ArcherWeaponRank,
        "MenAtArms" => army.MAAWeaponRank,
        _ => 0
    };


    private static void SetTroopWeaponRank(Army army, string type, int rank)
    {
        switch (type)
        {
            case "HeavyCavalry": army.HCWeaponRank = rank; break;
            case "LightCavalry": army.LCWeaponRank = rank; break;
            case "HeavyInfantry": army.HIWeaponRank = rank; break;
            case "LightInfantry": army.LIWeaponRank = rank; break;
            case "Archers": army.ArcherWeaponRank = rank; break;
            case "MenAtArms": army.MAAWeaponRank = rank; break;
        }
    }


    private static int TroopArmourRank(Army army, string type) => type switch
    {
        "HeavyCavalry" => army.HCArmourRank,
        "LightCavalry" => army.LCArmourRank,
        "HeavyInfantry" => army.HIArmourRank,
        "LightInfantry" => army.LIArmourRank,
        "Archers" => army.ArcherArmourRank,
        "MenAtArms" => army.MAAArmourRank,
        _ => 0
    };


    private static void SetTroopArmourRank(Army army, string type, int rank)
    {
        switch (type)
        {
            case "HeavyCavalry": army.HCArmourRank = rank; break;
            case "LightCavalry": army.LCArmourRank = rank; break;
            case "HeavyInfantry": army.HIArmourRank = rank; break;
            case "LightInfantry": army.LIArmourRank = rank; break;
            case "Archers": army.ArcherArmourRank = rank; break;
            case "MenAtArms": army.MAAArmourRank = rank; break;
        }
    }


    private static int MaterialStock(Nation nation, string material) => material.ToLower() switch
    {
        "leather" => nation.Leather,
        "bronze" => nation.Bronze,
        "steel" => nation.Steel,
        "mithril" => nation.Mithril,
        "timber" => nation.Timber,
        _ => 0
    };


    private static void ConsumeMaterial(Nation nation, string material, int amount)
    {
        switch (material.ToLower())
        {
            case "leather": nation.Leather = Math.Max(0, nation.Leather - amount); break;
            case "bronze": nation.Bronze = Math.Max(0, nation.Bronze - amount); break;
            case "steel": nation.Steel = Math.Max(0, nation.Steel - amount); break;
            case "mithril": nation.Mithril = Math.Max(0, nation.Mithril - amount); break;
            case "timber": nation.Timber = Math.Max(0, nation.Timber - amount); break;
        }
    }


    private object ProcessUpgradeWeapons(Order order, Dictionary<string, JsonElement> parameters)
        => ProcessUpgradeEquipment(order, parameters, isWeapon: true);


    private object ProcessUpgradeArmour(Order order, Dictionary<string, JsonElement> parameters)
        => ProcessUpgradeEquipment(order, parameters, isWeapon: false);


    private static readonly Dictionary<string, string> TroopShortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" },
        { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" }
    };


    private static int ParamTroops(Dictionary<string, JsonElement> p, string key)
    {
        if (p.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number)
            return Math.Max(0, el.GetInt32());
        return 0;
    }


    private object ProcessUpgradeEquipment(Order order, Dictionary<string, JsonElement> parameters, bool isWeapon)
    {
        if (order.Army == null) return MakeResult(order, "No army specified", false);
        var kind = isWeapon ? "Weapons" : "Armour";

        var material = isWeapon ? "bronze" : "leather";
        if (parameters.TryGetValue("material", out var matEl)) material = matEl.GetString() ?? material;
        if (!MaterialRank.TryGetValue(material, out var targetRank))
            return MakeResult(order, "Material must be leather, bronze, steel or mithril", false);

        var wanted = TroopShortKeys
            .Where(kv => ParamTroops(parameters, kv.Key) > 0)
            .Select(kv => kv.Value)
            .ToList();
        if (wanted.Count == 0)
            return MakeResult(order, "Specify troops per type (hc/lc/hi/li/ar/ma)", false);
        var missing = wanted.Where(t => TroopCount(order.Army!, t) <= 0).ToList();
        if (missing.Count > 0)
            return MakeResult(order, $"No {string.Join(", ", missing)} troops in {order.Army.Name}", false);

        var amount = wanted.Sum(t => ParamTroops(parameters, TroopShortKeys.First(kv => kv.Value == t).Key));
        var materialUnits = Math.Max(1, (amount + 99) / 100);

        // Consume from army Train spare equipment, not nation stock
        var spareCount = isWeapon ? order.Army.SpareWeapons : order.Army.SpareArmour;
        var spareMaterial = isWeapon ? order.Army.SpareWeaponsMaterial : order.Army.SpareArmourMaterial;
        var spareRank = MaterialRank.GetValueOrDefault(spareMaterial ?? "", 0);
        if (spareCount < materialUnits || spareRank < targetRank)
            return MakeResult(order, $"Insufficient {kind.ToLower()} in Train: need {materialUnits} {material} (have {spareCount} {spareMaterial} rank {spareRank})", false);

        var upgraded = new List<string>();
        foreach (var t in wanted)
        {
            var current = isWeapon ? TroopWeaponRank(order.Army, t) : TroopArmourRank(order.Army, t);
            if (current >= targetRank) continue;
            if (isWeapon) SetTroopWeaponRank(order.Army, t, targetRank);
            else SetTroopArmourRank(order.Army, t, targetRank);
            upgraded.Add(t);
        }
        if (upgraded.Count == 0)
            return MakeResult(order, $"{kind} already at {material} rank or better", false);

        // Consume from army Train
        if (isWeapon)
        {
            order.Army.SpareWeapons -= materialUnits;
            if (order.Army.SpareWeapons <= 0) order.Army.SpareWeaponsMaterial = "none";
        }
        else
        {
            order.Army.SpareArmour -= materialUnits;
            if (order.Army.SpareArmour <= 0) order.Army.SpareArmourMaterial = "none";
        }

        var goldCost = isWeapon ? 500 : 600;
        order.Nation.Gold -= goldCost;
        order.Status = "resolved";
        order.Result = $"{kind} upgraded to {material} for {string.Join(", ", upgraded)} ({amount} troops, {goldCost} gold, {materialUnits} {material} from Train)";
        return MakeResult(order, order.Result);
    }


    // â”€â”€ RECLUTAMIENTO EXTRA â”€â”€

    private object ProcessRetireTroops(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Army == null) return MakeResult(order, "No army", false);

        var total = 0;
        foreach (var kv in TroopShortKeys)
        {
            var n = ParamTroops(parameters, kv.Key);
            if (n <= 0) continue;
            var have = TroopCount(order.Army, kv.Value);
            var retire = Math.Min(have, n);
            SetTroopCount(order.Army, kv.Value, have - retire);
            total += retire;
        }
        if (total <= 0) return MakeResult(order, "No troops retired (amounts per type: hc/lc/hi/li/ar/ma)", false);
        order.Nation.Gold += total * 10;
        order.Status = "resolved";
        order.Result = $"Retired {total} troops for {total * 10} gold";
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

        var material = "steel";
        if (parameters.TryGetValue("material", out var matEl)) material = matEl.GetString() ?? material;
        if (!MaterialRank.TryGetValue(material, out _) || (material != "leather" && material != "bronze" && material != "steel" && material != "mithril"))
            return MakeResult(order, "Armour material must be leather, bronze, steel or mithril", false);

        // Each unit of spare armour costs gold + leather + steel
        var goldCost = amount * 5;
        var leatherCost = amount * 5;
        var steelCost = amount * 2;
        if (order.Nation.Gold < goldCost || order.Nation.Leather < leatherCost || order.Nation.Steel < steelCost)
            return MakeResult(order, $"Insufficient resources to forge {amount} {material} armour (need {goldCost}g, {leatherCost} leather, {steelCost} steel)", false);

        // If Train already has spare armour of a different material, only add if same material
        var army = order.Army;
        if (army.SpareArmour > 0 && army.SpareArmourMaterial != material && army.SpareArmourMaterial != "none")
            return MakeResult(order, $"Train already has {army.SpareArmour} {army.SpareArmourMaterial} armour (cannot mix materials)", false);

        order.Nation.Gold -= goldCost;
        order.Nation.Leather -= leatherCost;
        order.Nation.Steel -= steelCost;
        army.SpareArmour += amount;
        army.SpareArmourMaterial = material;
        order.Status = "resolved";
        order.Result = $"Forged {amount} {material} armour for Train at {pc.Name} (now {army.SpareArmour} {material} in Train)";
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

        var material = "bronze";
        if (parameters.TryGetValue("material", out var matEl)) material = matEl.GetString() ?? material;
        if (!MaterialRank.TryGetValue(material, out _) || (material != "bronze" && material != "steel" && material != "mithril"))
            return MakeResult(order, "Weapon material must be bronze, steel or mithril", false);

        // Each unit of spare weapon costs gold + bronze + steel
        var goldCost = amount * 5;
        var bronzeCost = amount * 3;
        var steelCost = amount;
        if (order.Nation.Gold < goldCost || order.Nation.Bronze < bronzeCost || order.Nation.Steel < steelCost)
            return MakeResult(order, $"Insufficient resources to forge {amount} {material} weapons (need {goldCost}g, {bronzeCost} bronze, {steelCost} steel)", false);

        var army = order.Army;
        if (army.SpareWeapons > 0 && army.SpareWeaponsMaterial != material && army.SpareWeaponsMaterial != "none")
            return MakeResult(order, $"Train already has {army.SpareWeapons} {army.SpareWeaponsMaterial} weapons (cannot mix materials)", false);

        order.Nation.Gold -= goldCost;
        order.Nation.Bronze -= bronzeCost;
        order.Nation.Steel -= steelCost;
        army.SpareWeapons += amount;
        army.SpareWeaponsMaterial = material;
        order.Status = "resolved";
        order.Result = $"Forged {amount} {material} weapons for Train at {pc.Name} (now {army.SpareWeapons} {material} in Train)";
        return MakeResult(order, order.Result);
        return MakeResult(order, order.Result);
    }
}
