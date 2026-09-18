using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Allegiance, relations, challenges and influence.</summary>
public sealed partial class TurnProcessor
{

    private object ProcessSpreadRumours(Order order, Dictionary<string, JsonElement> parameters)
    {
        order.Status = "resolved";
        order.Result = "Rumours spread";
        return MakeResult(order, order.Result);
    }


    // 520: automática, +1-10 lealtad propia y +1-5 emisario.
    private object ProcessInfluenceOwn(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var hex = order.Character.LocationHex;
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at one of your population centres", false);
        if (order.Character.EmissarySkill <= 0) return MakeResult(order, "Needs emissary skill", false);
        pc.Loyalty = Math.Min(100, pc.Loyalty + _rng.Next(1, 11));
        order.Character.EmissarySkill = Math.Min(100, order.Character.EmissarySkill + _rng.Next(1, 6));
        order.Status = "resolved";
        order.Result = $"Loyalty at {pc.Name} raised to {pc.Loyalty}";
        return MakeResult(order, order.Result);
    }


    private static void SetMutualRelation(Order order, Game game, string otherNationId, int level)
    {
        var fwd = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == otherNationId);
        if (fwd == null)
        {
            fwd = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = order.NationId, TargetNationId = otherNationId };
            order.Nation.Relations.Add(fwd);
        }
        fwd.Level = level;
        var backNation = game.Nations.FirstOrDefault(n => n.Id == otherNationId);
        if (backNation == null) return;
        var back = backNation.Relations.FirstOrDefault(r => r.TargetNationId == order.NationId);
        if (back == null)
        {
            back = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = otherNationId, TargetNationId = order.NationId };
            backNation.Relations.Add(back);
        }
        back.Level = level;
    }


    // 525: +d6>=12, -5-15 lealtad ajena y +1-10 emisario. Sin toma de control
    // (el reglamento solo da "posibilidad" sin fórmula).
    private object ProcessInfluenceOther(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (order.Character.EmissarySkill <= 0) return MakeResult(order, "Needs emissary skill", false);
        var hex = order.Character.LocationHex;
        var pc = GetPC(game, hex);
        if (pc == null) return MakeResult(order, "No population centre here", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Use 520 on your own centres", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        bool enemyPresent = game.Nations
            .SelectMany(n => n.Armies.Select(a => new { a.LocationHex, NationId = n.Id })
                .Concat(n.Navies.Select(v => new { v.LocationHex, NationId = n.Id })))
            .Any(u => u.LocationHex == hex && u.NationId != order.NationId
                && game.Nations.FirstOrDefault(n => n.Id == u.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == order.NationId)?.Level <= -1);
        if (enemyPresent) return MakeResult(order, "Enemy forces present", false);
        int roll = order.Character.EmissarySkill + _rng.Next(1, 7);
        if (roll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Influence failed at {pc.Name} (roll {roll})";
            return MakeResult(order, order.Result);
        }
        pc.Loyalty = Math.Max(0, pc.Loyalty - _rng.Next(5, 16));
        order.Character.EmissarySkill = Math.Min(100, order.Character.EmissarySkill + _rng.Next(1, 11));
        // La nación objetivo se vuelve neutral hacia nosotros (ambas direcciones).
        SetMutualRelation(order, game, pc.NationId, 0);
        order.Status = "resolved";
        order.Result = $"Loyalty at {pc.Name} lowered to {pc.Loyalty} (roll {roll}); relations now neutral";
        return MakeResult(order, order.Result);
    }


    private object ProcessChangeAllegiance(Order order, Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("allegiance", out var el))
            return MakeResult(order, "Missing allegiance", false);
        var allegiance = (el.GetString() ?? "").ToLower();
        if (allegiance != "free_peoples" && allegiance != "dark_servants" && allegiance != "neutral")
            return MakeResult(order, "Allegiance must be free_peoples, dark_servants or neutral", false);
        if (order.Nation.Allegiance != "neutral")
            return MakeResult(order, "Only neutral nations can change allegiance", false);
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
        order.Nation.Allegiance = allegiance;
        order.Status = "resolved";
        order.Result = $"Allegiance changed to {order.Nation.Allegiance}";
        return MakeResult(order, order.Result);
    }


    private object ProcessUpgradeRelations(Order order, Dictionary<string, JsonElement> p)
    {
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
        if (!p.TryGetValue("nationId", out var nid)) return MakeResult(order, "Missing nationId", false);
        var tid = nid.GetString()!;
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == tid);
        if (rel == null)
        {
            rel = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = order.NationId, TargetNationId = tid, Level = 0 };
            order.Nation.Relations.Add(rel);
        }
        rel.Level = Math.Min(100, rel.Level + 10);
        order.Status = "resolved"; order.Result = $"Relations improved to {rel.Level}";
        return MakeResult(order, order.Result);
    }


    private object ProcessDowngradeRelations(Order order, Dictionary<string, JsonElement> p)
    {
        if (!AtCapital(order))
            return MakeResult(order, "Must be at your own capital", false);
        if (!p.TryGetValue("nationId", out var nid)) return MakeResult(order, "Missing nationId", false);
        var tid = nid.GetString()!;
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == tid);
        if (rel == null)
        {
            rel = new NationRelation { Id = Guid.NewGuid().ToString(), NationId = order.NationId, TargetNationId = tid, Level = 0 };
            order.Nation.Relations.Add(rel);
        }
        rel.Level = Math.Max(-100, rel.Level - 10);
        order.Status = "resolved"; order.Result = $"Relations worsened to {rel.Level}";
        return MakeResult(order, order.Result);
    }


    private object ProcessIssueChallenge(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No challenger", false);
        if (!p.TryGetValue("targetId", out var tid)) return MakeResult(order, "Missing targetId", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == tid.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);

        // Deben estar en la misma casilla
        if (order.Character.LocationHex != target.LocationHex)
            return MakeResult(order, $"Target not in the same hex (target at {target.LocationHex})", false);

        // Negativa: si el objetivo emitiÃ³ la orden 215 (Rechazar desafÃ­os) este turno, se evita el duelo
        var currentTurn = game.Turns.FirstOrDefault(t => t.Status == "processing")
                          ?? game.Turns.FirstOrDefault(t => t.Status == "orders_open");
        var refused = currentTurn?.Orders.Any(o => o.CharacterId == target.Id && o.Code == 215) ?? false;
        if (refused)
        {
            order.Character.ChallengeRank += 2;
            target.ChallengeRank = Math.Max(0, target.ChallengeRank - 2);
            order.Status = "resolved";
            order.Result = $"Challenge refused by {target.Name}; {order.Character.Name} wins by forfeit (+2 CR), {target.Name} loses 2 CR";
            return MakeResult(order, order.Result);
        }

        var duel = _combat.ResolveDuel(order.Character, target);
        order.Status = "resolved"; order.Result = duel.Message;
        return MakeResult(order, duel.Message);
    }


    private object ProcessRefuseChallenges(Order order, Dictionary<string, JsonElement> p)
    {
        order.Status = "resolved"; order.Result = "All personal challenges refused this turn";
        return MakeResult(order, order.Result);
    }


    // Fase de desafÃ­os personales (reglamento G-1):
    //  - Un solo duelo por personaje por turno (retador y objetivo).
    //  - En la misma casilla; no contra personajes de la propia naciÃ³n.
    //  - Si varios retan al mismo objetivo, lucha el de mayor rango natural.
    //  - Si el objetivo emitiÃ³ 215 (y no tambiÃ©n 210), el desafÃ­o se rechaza (sin duelo).
    private void ResolvePersonalChallenges(Game game, Turn currentTurn, List<object> results)
    {
        var challengeOrders = currentTurn.Orders
            .Where(o => o.Code == 210 && o.Status == "pending" && o.Character != null)
            .ToList();
        if (challengeOrders.Count == 0) return;

        // Personajes que rechazan desafÃ­os este turno (215), salvo que tambiÃ©n hayan emitido 210
        var refused = new HashSet<string>(
            currentTurn.Orders
                .Where(o => o.Code == 215 && o.Status == "pending" && o.Character != null)
                .Select(o => o.CharacterId)
                .Distinct());
        var challengerIds = new HashSet<string>(challengeOrders.Select(o => o.CharacterId));
        refused.ExceptWith(challengerIds); // 215 ignorada si tambiÃ©n emitiÃ³ 210

        var fought = new HashSet<string>();

        foreach (var grp in challengeOrders.GroupBy(o => GetTargetId(o)).Where(g => g.Key != null))
        {
            var target = FindCharacter(game, grp.Key!);
            if (target == null)
            {
                foreach (var o in grp) MarkResolved(o, "Target not found", false, results);
                continue;
            }

            // Retadores ordenados por Challenge Rank descendente; lucha el de mayor CR.
            foreach (var order in grp.OrderByDescending(o => o.Character!.ChallengeRank))
            {
                var challenger = order.Character!;
                if (fought.Contains(challenger.Id) || fought.Contains(target.Id))
                {
                    MarkResolved(order, "Character already fought a personal challenge this turn", false, results);
                    continue;
                }
                if (challenger.NationId == target.NationId)
                {
                    MarkResolved(order, "Cannot challenge a character of your own nation", false, results);
                    continue;
                }
                if (challenger.LocationHex != target.LocationHex)
                {
                    MarkResolved(order, $"Target not in the same hex ({target.LocationHex})", false, results);
                    continue;
                }
                if (target.ArmyId != null && challenger.ArmyId == null)
                {
                    MarkResolved(order, "Cannot challenge an army commander unless you command an army", false, results);
                    continue;
                }
                if (refused.Contains(target.Id))
                {
                    var gain = _rng.Next(1, 16);
                    CombatResolver.IncreaseBestSkill(challenger, gain);
                    challenger.ChallengeRank = _combat.CalculateChallengeRank(challenger);
                    CombatResolver.ApplyCommanderMorale(challenger, gain);
                    CombatResolver.ApplyCommanderMorale(target, -gain);
                    MarkResolved(order, $"Challenge refused by {target.Name}; {challenger.Name} gains +{gain} best skill (no duel)", true, results);
                    fought.Add(challenger.Id); fought.Add(target.Id);
                    continue;
                }

                var duel = _combat.ResolveDuel(challenger, target);
                MarkResolved(order, duel.Message, true, results);
                fought.Add(challenger.Id); fought.Add(target.Id);
                if (challenger.IsDead) HandleCommanderDeath(game, challenger, results);
                if (target.IsDead) HandleCommanderDeath(game, target, results);
                break; // un solo retador por objetivo
            }
        }
    }


    // Reglamento pÃ¡g. 86: si un ejÃ©rcito pierde a su comandante en el duelo y ningÃºn
    // otro personaje con rango de mando (CommandSkill >= 1) puede reemplazarlo, el
    // ejÃ©rcito se desbanda (rout).
    private void HandleCommanderDeath(Game game, Character deadCommander, List<object> results)
    {
        var army = game.Nations.SelectMany(n => n.Armies)
            .FirstOrDefault(a => a.CommanderId == deadCommander.Id);
        if (army == null) return;

        var replacement = army.Characters
            .Where(c => !c.IsDead && c.Id != deadCommander.Id && c.CommandSkill >= 1)
            .OrderByDescending(c => c.CommandSkill)
            .ThenByDescending(c => c.ChallengeRank)
            .FirstOrDefault();

        if (replacement != null)
        {
            army.CommanderId = replacement.Id;
            results.Add(new { type = "commander", armyId = army.Id, message = $"{replacement.Name} assumes command of {army.Name} after {deadCommander.Name}'s death" });
        }
        else
        {
            foreach (var c in army.Characters.Where(c => c.Id != deadCommander.Id).ToList())
                c.ArmyId = null;
            _db.Armies.Remove(army);
            results.Add(new { type = "commander", armyId = army.Id, message = $"{army.Name} routs and is disbanded (commander {deadCommander.Name} slain, no replacement with command rank)" });
        }
    }


    private static string? GetTargetId(Order o)
    {
        try
        {
            using var doc = JsonDocument.Parse(o.Parameters);
            if (doc.RootElement.TryGetProperty("targetId", out var el))
                return el.GetString();
        }
        catch { }
        return null;
    }


    private static Character? FindCharacter(Game game, string id)
        => game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == id);


    private void MarkResolved(Order order, string msg, bool success, List<object> results)
    {
        order.Status = "resolved";
        order.Result = msg;
        results.Add(MakeResult(order, msg, success));
    }
}
