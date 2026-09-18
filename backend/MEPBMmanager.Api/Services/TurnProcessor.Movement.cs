using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Moving characters, armies, companies and navies.</summary>
public sealed partial class TurnProcessor
{

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
}
