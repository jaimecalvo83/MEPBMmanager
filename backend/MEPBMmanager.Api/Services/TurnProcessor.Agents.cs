using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Espionage, sabotage, scouting and encounters.</summary>
public sealed partial class TurnProcessor
{

    // â”€â”€ MISC â”€â”€

    private object ProcessReactionEncounter(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var enc = _db.Encounters.FirstOrDefault(e => !e.IsResolved && e.LocationHex == ch.LocationHex &&
                                                     (e.CharacterId == ch.Id || e.CharacterId == null));
        if (enc == null) return MakeResult(order, "No encounter to react to here", false);

        int roll = ch.CommandSkill + _rng.Next(1, 7);
        enc.IsResolved = true;
        if (roll >= 10)
        {
            int gold = _rng.Next(50, 300);
            order.Nation.Gold += gold;
            enc.Result = $"Defeated the {enc.Type} (roll {roll}); gained {gold} gold";
        }
        else
        {
            int dmg = _rng.Next(10, 40);
            ch.Health = Math.Max(0, ch.Health - dmg);
            if (ch.Health <= 0) { ch.IsDead = true; ch.ArmyId = null; }
            enc.Result = $"Overwhelmed by the {enc.Type} (roll {roll}); took {dmg} damage";
        }
        order.Status = "resolved";
        order.Result = enc.Result;
        return MakeResult(order, enc.Result);
    }


    private object ProcessInvestigateEncounter(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var enc = _db.Encounters.FirstOrDefault(e => !e.IsResolved && e.LocationHex == ch.LocationHex &&
                                                     (e.CharacterId == ch.Id || e.CharacterId == null));
        if (enc == null) return MakeResult(order, "No encounter to investigate here", false);

        int roll = ch.AgentSkill + _rng.Next(1, 7);
        enc.IsResolved = true;
        if (roll < 10)
        {
            enc.Result = $"Investigation failed (roll {roll})";
            order.Status = "resolved";
            order.Result = enc.Result;
            return MakeResult(order, enc.Result);
        }

        switch (enc.Type)
        {
            case "artifact":
                var foundTypes = CombatArtifactTypes.ToArray();
                _db.Artifacts.Add(new Artifact
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = order.NationId,
                    Name = "Recovered Artifact",
                    Type = foundTypes[_rng.Next(foundTypes.Length)],
                    Alignment = "none",
                    Bonus = _rng.Next(100, 500),
                    IsAtCapital = false,
                    HeldByCharacterId = ch.Id,
                });
                enc.Result = $"Found an artifact (roll {roll})";
                break;
            case "lore":
                if (ch.MageSkill > 0)
                {
                    var lorePool = SpellCatalog.OfType(SpellType.Lore).Where(s => !s.IsLost).ToList();
                    if (lorePool.Count > 0)
                    {
                        var learned = lorePool[_rng.Next(lorePool.Count)];
                        _db.Spells.Add(new Spell
                        {
                            Id = Guid.NewGuid().ToString(),
                            CharacterId = ch.Id,
                            SpellId = learned.Id,
                            IsKnown = true,
                            IsLost = false,
                            Rank = 0
                        });
                        enc.Result = $"Learned lore: {learned.Name} (roll {roll})";
                        break;
                    }
                }
                enc.Result = $"Learned lore (roll {roll})";
                break;
            case "creature":
                order.Nation.Gold += _rng.Next(20, 150);
                enc.Result = $"Bested the creature (roll {roll})";
                break;
            default:
                enc.Result = $"Investigation successful (roll {roll})";
                break;
        }
        order.Status = "resolved";
        order.Result = enc.Result;
        return MakeResult(order, enc.Result);
    }


    // â”€â”€ EMISSARY EXTRA â”€â”€

    // 500: espía en UNA nación a través de un personaje objetivo con skill e/a
    // en el mismo hex. Solo un activo por nación y turno (GameEvent).
    private object ProcessRecruitDoubleAgent(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (order.Character.EmissarySkill <= 0)
            return MakeResult(order, "Needs emissary skill", false);
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "Missing targetId: double agents are recruited through a character", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == tgtEl.GetString());
        if (target == null || target.IsDead) return MakeResult(order, "Target not found", false);
        if (target.EmissarySkill <= 0 && target.AgentSkill <= 0)
            return MakeResult(order, $"{target.Name} has no emissary or agent skill", false);
        if (target.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
        if (target.NationId == order.NationId)
            return MakeResult(order, "Cannot plant a double agent in your own nation", false);
        var targetNation = game.Nations.FirstOrDefault(n => n.Id == target.NationId);
        if (targetNation == null) return MakeResult(order, "Target nation not found", false);

        var existing = _db.GameEvents.FirstOrDefault(e => e.GameId == order.GameId
            && e.Type == "double_agent" && e.Data.Contains(target.NationId));
        if (existing != null)
            return MakeResult(order, $"Already have a double agent in {targetNation.Name}", false);

        var roll = order.Character.EmissarySkill + _rng.Next(1, 7);
        var success = roll >= 12;

        order.Status = "resolved";
        if (!success)
        {
            order.Result = $"Recruitment failed in {targetNation.Name} (roll {roll})";
            return MakeResult(order, order.Result);
        }
        _db.GameEvents.Add(new GameEvent
        {
            Id = Guid.NewGuid().ToString(),
            GameId = order.GameId,
            TurnId = order.TurnId,
            Type = "double_agent",
            Data = $"{{\"nationId\":\"{target.NationId}\",\"characterId\":\"{target.Id}\",\"by\":\"{order.Character.Id}\"}}"
        });
        order.Result = $"Double agent recruited in {targetNation.Name} via {target.Name} (roll {roll})";
        return MakeResult(order, order.Result);
    }


    // 505: soborno/reclutamiento sobre personajes de naciones NO jugadas del mismo
    // bando. Si se cumplen (mismo bando, nación sin jugador, <21 pjs) el personaje
    // se une; si no, queda como agente sobornado un turno y revela información.
    private object ProcessBribeCharacter(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "No target specified", false);

        var target = _db.Characters.Include(c => c.Nation).FirstOrDefault(c => c.Id == tgtEl.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);
        if (target.NationId == order.NationId) return MakeResult(order, "Cannot bribe your own character", false);
        if (target.IsKidnapped) return MakeResult(order, "Cannot bribe a hostage", false);
        if (order.Character == null || order.Character.EmissarySkill <= 0)
            return MakeResult(order, "Needs emissary skill", false);
        if (target.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);

        var amount = 500;
        if (parameters.TryGetValue("amount", out var amtEl)) amount = Math.Max(500, amtEl.GetInt32());
        if (order.Nation.Gold < amount)
        {
            order.Status = "failed";
            order.Result = $"Insufficient gold: need {amount}";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= amount;
        var roll = order.Character.EmissarySkill + _rng.Next(1, 7) + Math.Min(10, amount / 1000);
        var success = roll >= 12;

        order.Status = "resolved";
        if (!success)
        {
            order.Result = $"Bribe failed (roll {roll}): {target.Name} refused";
            return MakeResult(order, order.Result);
        }
        order.Character.EmissarySkill = Math.Min(100, order.Character.EmissarySkill + _rng.Next(1, 11));

        var targetPlayed = game.Players.Any(p => p.NationId == target.NationId);
        var sameAllegiance = target.Nation != null && target.Nation.Allegiance == order.Nation.Allegiance;
        var nationSize = game.Nations.SelectMany(n => n.Characters).Count(c => c.NationId == order.NationId && !c.IsDead);
        if (sameAllegiance && !targetPlayed && nationSize < 21)
        {
            var from = target.Nation?.Name ?? "?";
            target.NationId = order.NationId;
            order.Result = $"Bribe successful (roll {roll}): {target.Name} recruited from {from} (+emissary skill)";
            return MakeResult(order, order.Result);
        }
        var intelNation = target.Nation;
        var intel = intelNation == null ? "?" :
            $"gold {intelNation.Gold}, {intelNation.PopulationCentres.Count} PCs ({string.Join(", ", intelNation.PopulationCentres.Select(p => p.Name))}), " +
            $"{intelNation.Armies.Sum(a => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms)} troops";
        order.Result = $"Bribe successful (roll {roll}): {target.Name} is your bribed agent for this turn (+emissary skill). Intel: {intel}";
        return MakeResult(order, order.Result);
    }


    private object ProcessUncoverSecrets(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var eff = NationAbilities.UncoverSkill(order.Nation?.Name, order.Character.EmissarySkill);
        var roll = eff + _rng.Next(1, 7);
        var success = roll >= 12;
        order.Status = "resolved";
        order.Result = success
            ? $"Secrets uncovered (roll {roll})"
            : $"No secrets found (roll {roll})";
        return MakeResult(order, order.Result);
    }


    private object ProcessScoutArmy(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (!p.TryGetValue("commanderId", out var cmdEl))
            return MakeResult(order, "Missing commanderId of the force to scout", false);
        var boss = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == cmdEl.GetString());
        if (boss == null || boss.IsDead) return MakeResult(order, "Commander not found", false);
        var forceArmy = game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
        var forceNavy = forceArmy == null
            ? game.Nations.SelectMany(n => n.Navies).FirstOrDefault(v => v.CommanderId == boss.Id)
            : null;
        var forceHex = forceArmy?.LocationHex ?? forceNavy?.LocationHex;
        if (forceHex == null) return MakeResult(order, $"{boss.Name} commands no force", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : forceHex;
        var follow = p.TryGetValue("follow", out var fEl)
            && (fEl.ValueKind == System.Text.Json.JsonValueKind.True
                || (fEl.ValueKind == System.Text.Json.JsonValueKind.String && (fEl.GetString() ?? "").ToLower() is "y" or "yes" or "true"));
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 905, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Scout of {boss.Name} at {hex}: nothing found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies).Where(a => a.LocationHex == hex)
            .Select(a => $"{a.Name} ({a.Nation.Name}) HC:{a.HeavyCavalry} HI:{a.HeavyInfantry}").ToList();
        order.Status = "resolved";
        order.Result = (seen.Count > 0 ? $"Scout of {boss.Name} at {hex}: {string.Join(", ", seen)}" : $"Scout of {boss.Name} at {hex}: nothing")
            + (follow ? " (following)" : "");
        return MakeResult(order, order.Result);
    }


    private object ProcessScoutPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character?.LocationHex ?? "");
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 920, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 10)
        {
            order.Status = "resolved";
            order.Result = $"Scout PC at {hex}: nothing found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.PopulationCentres).Where(pc => pc.LocationHex == hex)
            .Select(pc => $"{pc.Name} ({pc.Nation?.Name ?? "?"}) L:{pc.Loyalty}").ToList();
        order.Status = "resolved";
        order.Result = seen.Count > 0 ? $"Scout PC at {hex}: {string.Join(", ", seen)}" : $"Scout PC at {hex}: nothing";
        return MakeResult(order, order.Result);
    }


    private object ProcessScoutCharacters(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (!IsLandHex(order.GameId, order.Character?.LocationHex))
            return MakeResult(order, "Must be on land", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character?.LocationHex ?? "");
        var eff = NationAbilities.ScoutSkill(order.Nation?.Name, 930, order.Character?.AgentSkill ?? 0, order.Character?.CommandSkill ?? 0);
        var sroll = eff + _rng.Next(1, 7);
        if (sroll < 12)
        {
            order.Status = "resolved";
            order.Result = $"Scout chars at {hex}: none found (roll {sroll})";
            return MakeResult(order, order.Result);
        }
        var seen = game.Nations.Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Characters).Where(c => c.LocationHex == hex && !c.IsDead)
            .Select(c => $"{c.Name} ({c.Nation.Name})").ToList();
        order.Status = "resolved";
        order.Result = seen.Count > 0 ? $"Scout chars at {hex}: {string.Join(", ", seen)}" : $"Scout chars at {hex}: none";
        return MakeResult(order, order.Result);
    }


    private object ProcessGuardLocation(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString()! : (order.Character.LocationHex);
        var gpc = game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(x => x.LocationHex == hex);
        if (gpc != null && gpc.NationId != order.NationId && gpc.IsHidden)
            return MakeResult(order, "No visible population centre here", false);
        var pc = game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(x => x.LocationHex == hex);
        if (pc != null && pc.NationId != order.NationId && pc.IsHidden)
            return MakeResult(order, "No visible population centre here", false);
        _db.Guards.Add(new Guard { Id = Guid.NewGuid().ToString(), CharacterId = order.Character.Id, TargetId = hex });
        order.Status = "resolved"; order.Result = $"Guarding location {hex}";
        return MakeResult(order, order.Result);
    }


    private object ProcessGuardCharacter(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (!p.TryGetValue("targetId", out var t)) return MakeResult(order, "Missing targetId", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == t.GetString());
        if (target == null || target.IsDead) return MakeResult(order, "Target not found", false);
        if (target.Id == order.Character.Id) return MakeResult(order, "Cannot guard yourself", false);
        if (target.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
        _db.Guards.Add(new Guard { Id = Guid.NewGuid().ToString(), CharacterId = order.Character.Id, TargetId = target.Id });
        order.Status = "resolved"; order.Result = $"Guarding {target.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessAssassinate(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No assassin", false);
        if (!p.TryGetValue("targetId", out var t)) return MakeResult(order, "Missing targetId", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == t.GetString());
        if (target == null) return MakeResult(order, "Target not found", false);
        if (target.NationId == order.NationId) return MakeResult(order, "Target must be of a different nation", false);
        if (target.IsKidnapped) return MakeResult(order, "Target is a hostage", false);
        if (target.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
        var effA = NationAbilities.AssassinSkill(order.Nation?.Name, order.Character.AgentSkill);
        var roll = effA + _rng.Next(1, 7);
        if (roll >= 12)
        {
            target.Health = 0; target.IsDead = true;
            order.Status = "resolved"; order.Result = $"Assassination succeeded: {target.Name} is dead (roll {roll})";
        }
        else
        {
            var dmg = _rng.Next(10, 40);
            target.Health = Math.Max(0, target.Health - dmg);
            order.Status = "resolved"; order.Result = $"Assassination failed (roll {roll}): {target.Name} took {dmg} damage";
        }
        return MakeResult(order, order.Result);
    }


    private object ProcessSabotageFort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Target must be of a different nation", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        if (string.IsNullOrEmpty(pc.Fortification)) return MakeResult(order, $"{pc.Name} has no fortifications", false);
        pc.Fortification = pc.Fortification switch { "fortress" => "castle", "castle" => "walls", "walls" => "palisade", "palisade" => null, _ => null };
        order.Status = "resolved"; order.Result = $"Sabotaged fortifications at {pc.Name} (now {pc.Fortification})";
        return MakeResult(order, order.Result);
    }


    private object ProcessSabotagePort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Target must be of a different nation", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        if (!pc.HasHarbour && !pc.HasPort) return MakeResult(order, $"{pc.Name} has no harbour or port", false);
        pc.HasPort = false; pc.HasHarbour = false;
        order.Status = "resolved"; order.Result = $"Sabotaged harbour/port at {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessSabotageProduction(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Target must be of a different nation", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        var store = "timber";
        if (p.TryGetValue("store", out var sEl)) store = sEl.GetString() ?? store;
        if (store != "timber" && store != "food" && store != "mounts" && store != "leather" && store != "bronze" && store != "steel" && store != "mithril")
            return MakeResult(order, "Store must be timber, food, mounts, leather, bronze, steel or mithril (not gold)", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : pc.Stores;
        pc.Stores = Math.Max(0, pc.Stores - amt);
        pc.Production = Math.Max(0, pc.Production - 50);
        order.Status = "resolved"; order.Result = $"Sabotaged production at {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessStealArtifact(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (!p.TryGetValue("artifactId", out var aId)) return MakeResult(order, "Missing artifactId", false);
        var art = _db.Artifacts.Find(aId.GetString());
        if (art == null) return MakeResult(order, "Artifact not found", false);
        if (art.HeldByCharacterId == order.Character?.Id)
            return MakeResult(order, "You already hold it", false);
        if (art.HeldByCharacterId != null)
        {
            var holder = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == art.HeldByCharacterId);
            if (holder == null) return MakeResult(order, "Holder not found", false);
            if (holder.NationId == order.NationId)
                return MakeResult(order, "Target must belong to another nation", false);
            if (holder.LocationHex != order.Character?.LocationHex)
                return MakeResult(order, "Artifact must be at the same hex", false);
        }
        else
        {
            if (art.LocationHex != order.Character?.LocationHex)
                return MakeResult(order, "Artifact must be at the same hex", false);
            var pc = game.Nations.SelectMany(n => n.PopulationCentres)
                .FirstOrDefault(x => x.LocationHex == art.LocationHex);
            if (pc != null && (pc.IsHidden || pc.NationId == order.NationId))
                return MakeResult(order, "Artifact must lie in a visible foreign population centre", false);
        }
        art.NationId = order.NationId;
        art.HeldByCharacterId = order.Character?.Id;
        art.IsAtCapital = order.Character == null;
        order.Status = "resolved"; order.Result = $"Stole artifact {art.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessStealGold(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (!p.TryGetValue("nationId", out var nid) || !p.TryGetValue("amount", out var aEl))
            return MakeResult(order, "Missing nationId/amount", false);
        var victim = game.Nations.FirstOrDefault(n => n.Id == nid.GetString());
        if (victim == null) return MakeResult(order, "Victim nation not found", false);
        if (victim.Id == order.NationId) return MakeResult(order, "Target must be of a different nation", false);
        var pc = game.Nations.SelectMany(n => n.PopulationCentres)
            .FirstOrDefault(x => x.LocationHex == order.Character?.LocationHex && x.NationId == victim.Id);
        if (pc == null || pc.IsHidden) return MakeResult(order, "Need a visible foreign population centre at your hex", false);
        var amt = Math.Min(victim.Gold, Math.Max(0, aEl.GetInt32()));
        victim.Gold -= amt; order.Nation.Gold += amt;
        order.Status = "resolved"; order.Result = $"Stole {amt} gold from {victim.Name}";
        return MakeResult(order, order.Result);
    }
}
