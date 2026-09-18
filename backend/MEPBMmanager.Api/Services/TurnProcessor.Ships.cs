using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Navies: building, scuttling and anchoring ships.</summary>
public sealed partial class TurnProcessor
{

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
        if (order.Character != null)
            order.Character.CommandSkill = Math.Min(100, order.Character.CommandSkill + 1);
        order.Status = "resolved";
        order.Result = $"Destroyed {destroyed} warships (+1 command)";
        return MakeResult(order, order.Result);
    }


    private object ProcessScuttleShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        var navy = order.Nation.Navies.FirstOrDefault();
        if (navy == null) return MakeResult(order, "No navy", false);

        // Compat: amount solo afectaba a transportes; warships/transports lo sustituyen.
        var warships = navy.Warships;
        var transports = navy.Transports;
        if (parameters.TryGetValue("warships", out var wEl) || parameters.TryGetValue("transports", out var tEl))
        {
            warships = parameters.TryGetValue("warships", out var w2) ? Math.Max(0, w2.GetInt32()) : 0;
            transports = parameters.TryGetValue("transports", out var t2) ? Math.Max(0, t2.GetInt32()) : 0;
        }
        else if (parameters.TryGetValue("amount", out var aEl))
        {
            warships = 0;
            transports = Math.Max(0, aEl.GetInt32());
        }
        warships = Math.Min(warships, navy.Warships);
        transports = Math.Min(transports, navy.Transports);
        navy.Warships -= warships;
        navy.Transports -= transports;
        order.Status = "resolved";
        order.Result = $"Scuttled {warships} warships and {transports} transports";
        return MakeResult(order, order.Result);
    }


    private object ProcessAbandonShips(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex ?? order.Navy?.LocationHex;
        var navy = order.Nation.Navies.FirstOrDefault(v => hex == null || v.LocationHex == hex) ?? order.Nation.Navies.FirstOrDefault();
        if (navy == null) return MakeResult(order, "No navy", false);

        var warships = navy.Warships;
        var transports = navy.Transports;
        if (parameters.TryGetValue("warships", out var wEl) || parameters.TryGetValue("transports", out var tEl))
        {
            warships = parameters.TryGetValue("warships", out var w2) ? Math.Min(Math.Max(0, w2.GetInt32()), navy.Warships) : 0;
            transports = parameters.TryGetValue("transports", out var t2) ? Math.Min(Math.Max(0, t2.GetInt32()), navy.Transports) : 0;
        }
        navy.Warships -= warships;
        navy.Transports -= transports;
        order.Status = "resolved";
        order.Result = $"Abandoned {warships} warships and {transports} transports at {navy.LocationHex}";
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
}
