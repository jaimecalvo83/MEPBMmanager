using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;

namespace MEPBMmanager.Api.Orders;

/// <summary>Live cost estimates: one small method per order family.</summary>
public sealed partial class OrderEstimateService
{
    public CostEstimate EstimateCosts(int code, EstimateScope scope)
    {
        var result = code switch
        {
            400 or 404 or 408 or 412 or 416 or 420 => RecruitCosts(code, scope),
            452 or 456 => ShipCosts(scope),
            440 => WarMachineCosts(scope),
            444 or 448 => ForgeCosts(code, scope),
            370 or 375 => UpgradeCosts(code, scope),
            494 => FortCosts(scope),
            530 or 535 or 552 or 555 or 745 or 950 or 505 or 660 or 725 or 728 or 731 or 734 or 737 => FixedCosts(code, scope),
            770 => HireCosts(scope),
            270 or 275 => NavyCapacity(code, scope),
            425 => RetireValue(scope),
            470 => StoreCapacity(scope),
            310 or 315 or 320 or 325 => MarketCosts(code, scope),
            _ => CostEstimate.Empty,
        };
        if (scope.Lang == "es")
        {
            var translated = new Dictionary<string, int>();
            foreach (var entry in result.Costs) translated[OrderTexts.CostKey(scope.Lang, entry.Key)] = entry.Value;
            return result with { Costs = translated };
        }
        return result;
    }

    private static int TotalTroops(Army army) => army.HeavyCavalry + army.LightCavalry + army.HeavyInfantry
        + army.LightInfantry + army.Archers + army.MenAtArms;

    private static int FortLadderIndex(string? fort) => (fort ?? "").ToLower() switch
    {
        "tower" or "palisade" => 0,
        "fort" => 1,
        "stone walls" or "castle" => 2,
        "walls" or "keep" or "fortress" => 3,
        "citadel walls" or "citadel" => 4,
        _ => -1
    };

    private static CostEstimate RecruitCosts(int code, EstimateScope scope)
    {
        var army = scope.SourceArmy();
        if (army == null) return CostEstimate.Empty;
        var pc = scope.OwnPopulationAt(army.LocationHex);
        if (pc == null) return CostEstimate.Empty;
        var unit = TurnProcessor.RecruitCostPerUnit[code];
        var used = scope.Used?.RecruitsByPc.GetValueOrDefault(pc.Id) ?? 0;
        var max = Math.Min(TurnProcessor.RecruitCapacity(pc.Size) - used, scope.Nation.Gold / unit);
        if (code is 400 or 404) max = Math.Min(max, scope.Nation.Mounts);
        var amount = Math.Min(scope.Number("amount", max), max);
        var costs = new Dictionary<string, int> { ["gold"] = amount * unit };
        if (code is 400 or 404) costs["mounts"] = amount;
        return new CostEstimate(costs, max, null);
    }

    private static CostEstimate ShipCosts(EstimateScope scope)
    {
        var army = scope.SourceArmy();
        var pc = army == null ? null : scope.OwnPopulationAt(army.LocationHex);
        if (pc == null || (!pc.HasPort && !pc.HasHarbour)) return CostEstimate.Empty;
        var timberEach = NationAbilities.ShipTimberCost(scope.Nation.Name);
        var max = Math.Min(scope.Nation.Timber / timberEach, scope.Nation.Gold / 1000);
        var amount = Math.Min(scope.Number("amount", max), max);
        return new CostEstimate(new Dictionary<string, int>
        {
            ["gold"] = amount * 1000,
            ["timber"] = amount * timberEach,
        }, max, null);
    }

    private static CostEstimate WarMachineCosts(EstimateScope scope)
    {
        var army = scope.SourceArmy();
        if (army == null || scope.OwnPopulationAt(army.LocationHex) == null) return CostEstimate.Empty;
        var nation = scope.Nation;
        var max = Math.Min(nation.Gold / 50, Math.Min(nation.Timber / 10, nation.Steel / 5));
        var amount = Math.Min(scope.Number("amount", max), max);
        return new CostEstimate(new Dictionary<string, int>
        {
            ["gold"] = amount * 50,
            ["timber"] = amount * 10,
            ["steel"] = amount * 5,
        }, max, null);
    }

    private static CostEstimate ForgeCosts(int code, EstimateScope scope)
    {
        var army = scope.SourceArmy();
        if (army == null || scope.OwnPopulationAt(army.LocationHex) == null) return CostEstimate.Empty;
        var rank = code == 444 ? army.HCArmourRank : army.HCWeaponRank;
        var headroom = Math.Max(0, 100 - rank);
        var nation = scope.Nation;
        var max = code == 444
            ? Math.Min(headroom, Math.Min(nation.Gold / 5, Math.Min(nation.Leather / 5, nation.Steel / 2)))
            : Math.Min(headroom, Math.Min(nation.Gold / 5, Math.Min(nation.Bronze / 3, nation.Steel)));
        var amount = Math.Min(scope.Number("amount", max), max);
        var costs = new Dictionary<string, int> { ["gold"] = amount * 5 };
        if (code == 444) { costs["leather"] = amount * 5; costs["steel"] = amount * 2; }
        else { costs["bronze"] = amount * 3; costs["steel"] = amount; }
        return new CostEstimate(costs, max, null);
    }

    private static CostEstimate UpgradeCosts(int code, EstimateScope scope)
    {
        var army = scope.SourceArmy();
        if (army == null) return CostEstimate.Empty;
        var material = (scope.Text("material") ?? "bronze").ToLower();
        var present = TotalTroops(army);
        var asked = TroopCodes.Sum(p => scope.Number(p.Short));
        var amount = Math.Min(asked <= 0 ? present : asked, present);
        return new CostEstimate(new Dictionary<string, int>
        {
            ["gold"] = code == 370 ? 500 : 600,
            [material] = Math.Max(1, (amount + 99) / 100),
        }, present, null);
    }

    private static CostEstimate FortCosts(EstimateScope scope)
    {
        var pc = scope.Text("pcId") != null
            ? scope.Nation.PopulationCentres.FirstOrDefault(p => p.Id == scope.Text("pcId"))
            : scope.OwnPopulationAt(scope.Text("hex") ?? scope.EffectiveLocation);
        if (pc == null) return CostEstimate.Empty;
        var ladder = new[] { "tower", "fort", "castle", "keep", "citadel" };
        var current = FortLadderIndex(pc.Fortification);
        if (current + 1 > 4) return CostEstimate.Empty;
        var next = ladder[current + 1];
        return new CostEstimate(new Dictionary<string, int>
        {
            ["timber"] = NationAbilities.FortTimberCost(scope.Nation.Name, next),
            ["gold"] = NationAbilities.FortGoldCost(next),
        }, null, null);
    }

    private static CostEstimate FixedCosts(int code, EstimateScope scope) => code switch
    {
        530 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 1500, ["timber"] = 2500 }, null, null),
        535 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 2500, ["timber"] = 5000 }, null, null),
        555 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 2000 }, null, null),
        552 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 4000 }, null, null),
        745 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 500 }, null, null),
        950 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 25000 }, null, null),
        505 => new CostEstimate(new Dictionary<string, int> { ["gold"] = Math.Max(500, scope.Number("amount", 500)) }, null, null),
        660 => new CostEstimate(new Dictionary<string, int> { ["gold"] = Math.Max(1000, scope.Number("amount", 1000)) }, null, null),
        725 => new CostEstimate(new Dictionary<string, int> { ["gold"] = 10000 }, null, null),
        _ => new CostEstimate(new Dictionary<string, int> { ["gold"] = 5000 }, null, null),
    };

    private static CostEstimate HireCosts(EstimateScope scope)
    {
        var costs = new Dictionary<string, int>
        {
            ["gold"] = NationAbilities.HireArmyCost(scope.Nation.Name)
        };
        var troops = Math.Max(0, scope.Number("troops", 0));
        if (troops > 0)
        {
            var troopType = scope.Text("troopType") ?? "MenAtArms";
            var full = TroopCodes.FirstOrDefault(p => p.Short.Equals(troopType, StringComparison.OrdinalIgnoreCase));
            if (full != default) troopType = full.Full;
            var materialUnits = Math.Max(1, (troops + 99) / 100);
            var food = Math.Max(0, scope.Number("food", 0));
            if (food > 0) costs["food"] = food;
            if (troopType is "HeavyCavalry" or "LightCavalry") costs["mounts"] = troops;
            costs[(scope.Text("weapons") ?? "bronze").ToLower()] = materialUnits;
            costs[(scope.Text("armour") ?? "leather").ToLower()] = materialUnits;
        }
        return new CostEstimate(costs, null, null);
    }

    private static CostEstimate NavyCapacity(int code, EstimateScope scope)
    {
        var navy = scope.Nation.Navies.FirstOrDefault();
        if (navy == null) return CostEstimate.Empty;
        return new CostEstimate(new Dictionary<string, int>(),
            code == 270 ? navy.Warships : navy.Transports, null);
    }

    private static CostEstimate RetireValue(EstimateScope scope)
    {
        var army = scope.SourceArmy();
        if (army == null) return CostEstimate.Empty;
        var total = TotalTroops(army);
        var amount = Math.Min(scope.Number("amount", total), total);
        return new CostEstimate(new Dictionary<string, int>(), total, amount * 10);
    }

    private static CostEstimate StoreCapacity(EstimateScope scope)
    {
        var pc = scope.OwnPopulationAt(scope.EffectiveLocation);
        if (pc == null) return CostEstimate.Empty;
        return new CostEstimate(new Dictionary<string, int>(), pc.Stores, null);
    }

    private static CostEstimate MarketCosts(int code, EstimateScope scope)
    {
        var product = scope.Text("product") ?? "";
        if (!TurnProcessor.IsMarketProductName(product, out var canonical)) return CostEstimate.Empty;
        var nation = scope.Nation;
        var used = scope.Used;
        if (code is 310 or 315)
        {
            var (_, baseBuy) = TurnProcessor.MarketRate(canonical);
            var buy = code == 310
                ? Math.Max(NationAbilities.MarketBuyPrice(nation.Name, baseBuy), scope.Number("price", NationAbilities.MarketBuyPrice(nation.Name, baseBuy)))
                : NationAbilities.MarketBuyPrice(nation.Name, baseBuy);
            var poolLeft = Math.Max(0, TurnProcessor.MarketBuyPoolFor(canonical) - (used?.BuyByProduct.GetValueOrDefault(canonical) ?? 0));
            var max = Math.Min(nation.Gold / Math.Max(1, buy), poolLeft);
            var amount = Math.Min(scope.Number("amount", max), max);
            return new CostEstimate(new Dictionary<string, int> { ["gold"] = amount * buy }, max, null);
        }
        var (_, baseSell) = TurnProcessor.MarketRate(canonical);
        var sell = NationAbilities.MarketSellPrice(nation.Name, baseSell);
        var stock = EstimateScope.StockLevel(nation, canonical);
        var sellCapLeft = TurnProcessor.MarketSellCapGold() - (used?.SellGoldUsed ?? 0);
        if (code == 320)
        {
            var amount = Math.Min(scope.Number("amount", stock), stock);
            return new CostEstimate(new Dictionary<string, int> { [canonical] = amount }, stock,
                Math.Max(0, Math.Min(amount * sell, sellCapLeft)));
        }
        var pct = scope.Number("percentage", 100);
        pct = Math.Clamp(pct, 0, 100);
        var allAmount = stock * pct / 100;
        return new CostEstimate(new Dictionary<string, int> { [canonical] = allAmount }, stock,
            Math.Max(0, Math.Min(allAmount * sell, sellCapLeft)));
    }
}
