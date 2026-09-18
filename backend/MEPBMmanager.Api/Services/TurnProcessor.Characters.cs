using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Naming, retiring and generic character orders.</summary>
public sealed partial class TurnProcessor
{

    private object ProcessRetireCharacter(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (order.Character.IsKidnapped) return MakeResult(order, "Cannot retire a hostage", false);

        order.Character.IsDead = true;
        order.Character.Health = 0;
        order.Status = "resolved";
        order.Result = $"{order.Character.Name} retired";
        return MakeResult(order, order.Result);
    }


    // â”€â”€ SKILL ORDERS (genÃ©rico) â”€â”€

    private object ProcessSkillOrder(Order order, string skillType, Dictionary<string, JsonElement> parameters)
    {
        if ((order.Code is 910 or 915 or 925) && !IsLandHex(order.GameId, order.Character?.LocationHex))
            return MakeResult(order, "Must be on land", false);
        var skill = (skillType, order.Code) switch
        {
            ("Command", 925) => NationAbilities.ScoutSkill(order.Nation?.Name, 925, order.Character.AgentSkill, order.Character.CommandSkill),
            ("Agent", 910) or ("Agent", 915) => NationAbilities.ScoutSkill(order.Nation?.Name, order.Code, order.Character.AgentSkill, order.Character.CommandSkill),
            ("Command", _) => order.Character.CommandSkill,
            ("Agent", _) => order.Character.AgentSkill,
            ("Emissary", _) => order.Character.EmissarySkill,
            ("Mage", _) => order.Character.MageSkill,
            _ => 8
        };

        var roll = skill + _rng.Next(1, 7);
        var target = 10;
        var success = roll >= target;

        if (success)
        {
            order.Character.ChallengeRank += 1;
            order.Status = "resolved";
            order.Result = $"{skillType} order succeeded (roll {roll})";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"{skillType} order failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }


    private object ProcessGenericOrder(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Order processed (basic resolution)";
        return MakeResult(order, $"Order {order.Code} processed");
    }


    private object ProcessNameCharacter(Order order, Dictionary<string, JsonElement> p, string type)
    {
        if (!p.TryGetValue("name", out var nEl)) return MakeResult(order, "Missing name", false);
        var newName = (nEl.GetString() ?? "").Trim();
        if (newName.Length < 5 || newName.Length > 17)
            return MakeResult(order, "Name must be 5-17 letters", false);
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
        // Reglamento: multi (725) 10000 oro; resto 5000.
        int cost = order.Code == 725 ? 10000 : 5000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        var capital = order.Nation.PopulationCentres.FirstOrDefault(x => x.IsCapital)
                      ?? order.Nation.PopulationCentres.FirstOrDefault();
        // Rango inicial 40 si la nación tiene NAME_<TIPO>_40; sigilo/desafío
        // extra (1d6) si tiene NEWCHAR_STEALTH / NEWCHAR_CHALLENGE.
        var startSkill = NationAbilities.NameCharacterSkill(order.Nation?.Name, type);
        var ch = new Character
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = newName,
            Type = type,
            LocationHex = capital?.LocationHex ?? "0,0",
            MaxHealth = 100,
            Health = 100,
            Stealth = NationAbilities.HasForNation(order.Nation?.Name, "NEWCHAR_STEALTH") ? _rng.Next(1, 7) : 0,
            ChallengeRank = NationAbilities.HasForNation(order.Nation?.Name, "NEWCHAR_CHALLENGE") ? _rng.Next(1, 7) : 0,
            CommandSkill = type == "commander" ? startSkill : RankParam(p, "command", 0),
            AgentSkill = type == "agent" ? startSkill : RankParam(p, "agent", 0),
            EmissarySkill = type == "emissary" ? startSkill : RankParam(p, "emissary", 0),
            MageSkill = type == "mage" ? startSkill : RankParam(p, "mage", 0)
        };
        _db.Characters.Add(ch);
        order.Nation.Gold -= cost;
        order.Status = "resolved"; order.Result = $"Named new {type}: {ch.Name} for {cost} gold";
        return MakeResult(order, order.Result);
    }
}
