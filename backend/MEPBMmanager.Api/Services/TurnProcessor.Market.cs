using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Caravans, market pools and nation logistics.</summary>
public sealed partial class TurnProcessor
{

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

    public static readonly Dictionary<int, int> RecruitCostPerUnit = new()
    {
        [400] = 200, [404] = 120, [408] = 150, [412] = 80, [416] = 100, [420] = 60
    };


    public static (int Buy, int Sell) MarketRate(string product) =>
        MarketPrice.TryGetValue((product ?? "").ToLower(), out var p) ? p : (0, 0);


    public static int MarketBuyPoolFor(string product) =>
        MarketBuyPool.TryGetValue((product ?? "").ToLower(), out var v) ? v : 0;


    public static int MarketSellCapGold() => MarketSellCap;


    public static IReadOnlyList<string> MarketProductList() => MarketProducts;


    public static bool IsMarketProductName(string? p, out string canon) => IsMarketProduct(p, out canon);


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
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
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
}
