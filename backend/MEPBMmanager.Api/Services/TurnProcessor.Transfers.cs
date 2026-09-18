using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Moving food, troops and gear between armies.</summary>
public sealed partial class TurnProcessor
{

    // â”€â”€ ECONOMÃA EXTRA â”€â”€

    private object ProcessTransferFoodArmyToArmy(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var src = order.Army;
        if (src == null) return MakeResult(order, "No source army", false);
        if ((!parameters.TryGetValue("targetArmyId", out var tgtEl) && !parameters.TryGetValue("destArmyId", out tgtEl)) || !parameters.TryGetValue("amount", out var amtEl))
            return MakeResult(order, "Missing targetArmyId or amount", false);
        var tgt = _db.Armies.Find(tgtEl.GetString());
        if (tgt == null) return MakeResult(order, "Target army not found", false);
        if (tgt.LocationHex != src.LocationHex)
            return MakeResult(order, "Both armies must be in the same hex", false);
        if (!SameOrFriendly(game, order.NationId, tgt.NationId))
            return MakeResult(order, "Target army must be of the same or a friendly nation", false);
        var amount = Math.Min(amtEl.GetInt32(), src.Food);
        src.Food -= amount;
        tgt.Food += amount;
        order.Status = "resolved";
        order.Result = $"Transferred {amount} food from {src.Name} to {tgt.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessTransferWarMachines(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "war");

    private object ProcessTransferWeapons(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "weapon");

    private object ProcessTransferArmour(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "armour");

    private object ProcessTransferTroops(Order order, Dictionary<string, JsonElement> p, Game game) => TransferBetweenArmies(order, p, game, "troops");


    private object TransferBetweenArmies(Order order, Dictionary<string, JsonElement> p, Game game, string kind)
    {
        if (order.Army == null) return MakeResult(order, "No source army", false);
        if ((!p.TryGetValue("targetArmyId", out var dEl) && !p.TryGetValue("destArmyId", out dEl)) || !p.TryGetValue("amount", out var aEl))
            return MakeResult(order, "Need targetArmyId and amount", false);
        var dest = GetArmyById(game, dEl.GetString()!);
        if (dest == null) return MakeResult(order, "Dest army not found", false);
        if (dest.LocationHex != order.Army.LocationHex)
            return MakeResult(order, "Both armies must be in the same hex", false);
        if (!SameOrFriendly(game, order.NationId, dest.NationId))
            return MakeResult(order, "Dest army must be of the same or a friendly nation", false);
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
                var moved = 0;
                foreach (var t in UpgradeableTroopTypes)
                {
                    if (moved >= amt) break;
                    var take = Math.Min(TroopCount(order.Army, t), amt - moved);
                    SetTroopCount(order.Army, t, TroopCount(order.Army, t) - take);
                    SetTroopCount(dest, t, TroopCount(dest, t) + take);
                    moved += take;
                }
                amt = moved;
                break;
        }
        order.Status = "resolved"; order.Result = $"Transferred {amt} {kind} to {dest.Name}";
        return MakeResult(order, order.Result);
    }


    private static void SetTroopCount(Army army, string type, int value)
    {
        switch (type)
        {
            case "HeavyCavalry": army.HeavyCavalry = value; break;
            case "LightCavalry": army.LightCavalry = value; break;
            case "HeavyInfantry": army.HeavyInfantry = value; break;
            case "LightInfantry": army.LightInfantry = value; break;
            case "Archers": army.Archers = value; break;
            case "MenAtArms": army.MenAtArms = value; break;
        }
    }
}
