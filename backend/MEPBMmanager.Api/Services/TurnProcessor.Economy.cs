using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Turn economy: taxes, upkeep, food consumption and growth.</summary>
public sealed partial class TurnProcessor
{

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
                var fromTrain = Math.Min(army.Food, foodCost);
                army.Food -= fromTrain;
                nation.Food -= foodCost - fromTrain;

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


    private static int GetArmyTotalTroops(Army army)
    {
        return army.HeavyCavalry + army.LightCavalry
             + army.HeavyInfantry + army.LightInfantry
             + army.Archers + army.MenAtArms;
    }


    private object ProcessChangeTaxRate(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
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


    private object ProcessTransferFoodToArmy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var army = order.Army;
        if (army == null) return MakeResult(order, "No army specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var pc = game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(p => p.LocationHex == army.LocationHex);
        if (pc == null || pc.IsHidden || pc.IsSieged
            || !(pc.NationId == order.NationId || SameOrFriendly(game, order.NationId, pc.NationId)))
            return MakeResult(order, "Need a non-hidden, non-sieged same or friendly population centre in the hex", false);

        var amount = amtEl.GetInt32();
        var actual = Math.Min(amount, order.Nation.Food);
        order.Nation.Food -= actual;
        army.Food += actual;
        order.Status = "resolved";
        order.Result = $"Transferred {actual} food to {army.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessTransferFoodToPC(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var army = order.Army;
        if (army == null) return MakeResult(order, "No army specified", false);
        if (!parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "No amount specified", false);

        var pc = game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(p => p.LocationHex == army.LocationHex);
        if (pc == null || pc.IsHidden || pc.IsSieged
            || !(pc.NationId == order.NationId || SameOrFriendly(game, order.NationId, pc.NationId)))
            return MakeResult(order, "Need a non-hidden, non-sieged same or friendly population centre in the hex", false);

        var amount = amtEl.GetInt32();
        var actual = Math.Min(amount, army.Food);
        army.Food -= actual;
        order.Nation.Food += actual;
        order.Status = "resolved";
        order.Result = $"Transferred {actual} food from {army.Name} to nation reserve";
        return MakeResult(order, order.Result);
    }


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
}
