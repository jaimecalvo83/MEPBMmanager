using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Kidnapping, custody and ransom.</summary>
public sealed partial class TurnProcessor
{

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
        if (target.NationId == order.NationId)
            return MakeResult(order, "Target must be of a different nation", false);
        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);

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

        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
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

        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
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

        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
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

        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
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
        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);

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
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);
        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);

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

        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
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


    private object ProcessTransferHostage(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "No target specified", false);

        var target = _db.Characters.Find(tgtEl.GetString());
        if (target == null || !target.IsKidnapped)
            return MakeResult(order, "Target not found or not kidnapped", false);
        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
        var receiver = parameters.TryGetValue("receiverId", out var rEl)
            ? game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == rEl.GetString()) : null;
        if (receiver != null)
        {
            if (receiver.LocationHex != order.Character?.LocationHex)
                return MakeResult(order, "Receiver must be at the same location", false);
            target.HeldByNationId = receiver.NationId;
        }

        if (order.Character != null)
            target.LocationHex = order.Character.LocationHex;

        order.Status = "resolved";
        order.Result = $"Transferred hostage {target.Name}" + (receiver != null ? $" to {receiver.Name}" : "");
        return MakeResult(order, order.Result);
    }


    private object ProcessOfferRansom(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
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
}
