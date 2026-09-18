using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Casting, researching and forgetting spells.</summary>
public sealed partial class TurnProcessor
{

    // â”€â”€ MAGE EXTRA â”€â”€

    private object ProcessCastHealSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Heal))
            return SpellUnknown(order, SpellType.Heal);

        if (!parameters.TryGetValue("spellId", out var sidEl)
            || SpellCatalog.Get(sidEl.GetInt32()) is not { } def
            || def.Type != SpellType.Heal || def.IsLost
            || !order.Character!.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, "Info must contain a valid known healing spell (spellId)", false);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 8;

        if (success && parameters.TryGetValue("targetId", out var tgtEl))
        {
            var target = _db.Characters.Find(tgtEl.GetString());
            if (target != null)
            {
                if (target.LocationHex != order.Character!.LocationHex)
                {
                    order.Status = "resolved";
                    order.Result = $"Heal fizzles: {target.Name} is not at the same location (proficiency still improves)";
                }
                else
                {
                    var healAmount = _rng.Next(20, 50);
                    target.Health = Math.Min(target.MaxHealth, target.Health + healAmount);
                    order.Status = "resolved";
                    order.Result = $"Heal successful (roll {roll}): {target.Name} healed {healAmount} HP (+1 mage skill)";
                }
                order.Character.MageSkill = Math.Min(100, order.Character.MageSkill + 1);
                var spell = order.Character.Spells.FirstOrDefault(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost);
                if (spell != null) spell.Rank = Math.Min(100, spell.Rank + _rng.Next(1, 6));
                return MakeResult(order, order.Result);
            }
        }

        order.Status = "resolved";
        order.Result = $"Heal failed (roll {roll})";
        return MakeResult(order, order.Result);
    }


    private object ProcessCastConjuringSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Conjuring))
            return SpellUnknown(order, SpellType.Conjuring);

        if (!parameters.TryGetValue("spellId", out var sidEl)
            || SpellCatalog.Get(sidEl.GetInt32()) is not { } def
            || def.Type != SpellType.Conjuring || def.IsLost
            || !order.Character!.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, "Info must contain a valid known conjuring spell (spellId)", false);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;
        var ch = order.Character;
        var mageRank = ch.MageSkill;

        if (!success)
        {
            order.Status = "resolved";
            order.Result = $"{def.Name} failed (roll {roll})";
            return MakeResult(order, order.Result);
        }

        ch.MageSkill = Math.Min(100, ch.MageSkill + 1);
        var spell = ch.Spells.FirstOrDefault(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost);
        if (spell != null) spell.Rank = Math.Min(100, spell.Rank + _rng.Next(1, 6));

        if (def.Id == 502 || def.Id == 504 || def.Id == 506)
        {
            var divisor = def.Id == 502 ? 3 : 2;
            if (!parameters.TryGetValue("targetId", out var tgtEl)
                || _db.Characters.Find(tgtEl.GetString()) is not { } target
                || target.IsDead)
                return MakeResult(order, $"{def.Name} failed: no valid target", false);

            var damage = Math.Max(1, mageRank / divisor);
            target.Health = Math.Max(0, target.Health - damage);
            order.Status = "resolved";
            order.Result = $"{def.Name} successful (roll {roll}): {target.Name} loses {damage} HP (+1 mage skill)";
        }
        else if (def.Id == 508)
        {
            var amount = parameters.TryGetValue("amount", out var amtEl) ? amtEl.GetInt32() : 0;
            var maxMounts = 5 * mageRank;
            var conjured = amount > 0 ? Math.Min(amount, maxMounts) : maxMounts;
            var pc = order.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == ch.LocationHex);
            if (pc == null)
                return MakeResult(order, $"{def.Name} failed: not at own population centre", false);

            order.Nation.Mounts += conjured;
            order.Status = "resolved";
            order.Result = $"{def.Name} successful (roll {roll}): created {conjured} mounts (+1 mage skill)";
        }
        else if (def.Id == 510)
        {
            var amount = parameters.TryGetValue("amount", out var amtEl) ? amtEl.GetInt32() : 0;
            var maxFood = 25 * mageRank;
            var conjured = amount > 0 ? Math.Min(amount, maxFood) : maxFood;
            var army = order.Game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.NationId == order.Nation.Id && a.LocationHex == ch.LocationHex);
            if (army != null)
            {
                army.Food += conjured;
                order.Status = "resolved";
                order.Result = $"{def.Name} successful (roll {roll}): created {conjured} food in {army.Name} (+1 mage skill)";
            }
            else
            {
                order.Nation.Food += conjured;
                order.Status = "resolved";
                order.Result = $"{def.Name} successful (roll {roll}): created {conjured} food (+1 mage skill)";
            }
        }
        else if (def.Id == 512)
        {
            var amount = parameters.TryGetValue("amount", out var amtEl) ? amtEl.GetInt32() : 0;
            var maxTroops = 5 * mageRank;
            var conjured = amount > 0 ? Math.Min(amount, maxTroops) : maxTroops;
            var army = order.Game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.NationId == order.Nation.Id && a.LocationHex == ch.LocationHex);
            if (army == null)
                return MakeResult(order, $"{def.Name} failed: not with an army or navy", false);

            army.MenAtArms += conjured;
            army.MAAWeaponRank = Math.Min(army.MAAWeaponRank, 10);
            army.MAAArmourRank = 0;
            army.MAATraining = Math.Min(army.MAATraining, 10);
            order.Status = "resolved";
            order.Result = $"{def.Name} successful (roll {roll}): created {conjured} men-at-arms in {army.Name} (wooden weapons, no armour, poor training) (+1 mage skill)";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"{def.Name}: unimplemented (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }


    private object ProcessCastMovementSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!KnowsSpell(order.Character, SpellType.Movement))
            return SpellUnknown(order, SpellType.Movement);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (success && parameters.TryGetValue("destination", out var destEl))
        {
            var dest = destEl.GetString();
            if (order.Army != null) order.Army.LocationHex = dest!;
            if (order.Character != null) order.Character.LocationHex = dest!;
            order.Status = "resolved";
            order.Result = $"Movement spell cast successfully (roll {roll}), moved to {dest}";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"Movement spell failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }


    private object ProcessResearchSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No mage for research", false);
        if (OwnedPCAt(order.Character.LocationHex, order.NationId) == null)
            return MakeResult(order, "Must be at one of your population centres to research", false);
        if (order.Character.Spells.Count(s => s.IsKnown && !s.IsLost) >= 15)
            return MakeResult(order, "Already knows 15 spells", false);

        // Hechizo objetivo: parámetro spellId o uno aleatorio aún no conocido.
        // Los perdidos exigen acceso nacional (LOST_SPELL_<id>).
        SpellDefinition def;
        if (parameters.TryGetValue("spellId", out var sidEl) && SpellCatalog.Get(sidEl.GetInt32()) is { } byId)
        {
            def = byId;
            if (def.IsLost && !NationAbilities.CanLearnLostSpell(order.Nation?.Name, def.Id))
                return MakeResult(order, $"Lost spell {def.Name} is not available to your nation", false);
        }
        else
        {
            var known = order.Character.Spells
                .Where(s => s.IsKnown && !s.IsLost)
                .Select(s => s.SpellId)
                .ToHashSet();
            var candidates = SpellCatalog.All
                .Where(s => !known.Contains(s.Id) && (!s.IsLost || NationAbilities.CanLearnLostSpell(order.Nation?.Name, s.Id)))
                .ToList();
            if (candidates.Count == 0)
                return MakeResult(order, "All spells already known", false);
            def = candidates[_rng.Next(candidates.Count)];
        }

        if (order.Character.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, $"Already know {def.Name}");

        order.Character.Spells.Add(new Spell
        {
            Id = Guid.NewGuid().ToString(),
            CharacterId = order.Character.Id,
            SpellId = def.Id,
            IsLost = def.IsLost,
            IsKnown = true
        });

        order.Status = "resolved";
        order.Result = $"Researched {def.Name} ({def.College} college)";
        return MakeResult(order, order.Result);
    }


    private object ProcessForgetSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character == null) return MakeResult(order, "No mage", false);

        var ids = IdList(parameters, "spellId");
        if (ids.Count == 0 || ids.Count > 6)
            return MakeResult(order, "Give 1-6 spellId(s) to forget", false);
        var forgotten = new List<string>();
        foreach (var idStr in ids)
        {
            if (!int.TryParse(idStr, out var sid)) continue;
            var known = order.Character.Spells.FirstOrDefault(s => s.SpellId == sid && s.IsKnown && !s.IsLost);
            if (known == null) continue;
            known.IsLost = true;
            known.IsKnown = false;
            forgotten.Add(SpellCatalog.Get(sid)?.Name ?? $"Spell {sid}");
        }
        if (forgotten.Count == 0)
            return MakeResult(order, "No known spells matched", false);
        order.Status = "resolved";
        order.Result = $"Forgot {string.Join(", ", forgotten)}";
        return MakeResult(order, order.Result);
    }


    private object ProcessPrenticeMagery(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Character != null && OwnedPCAt(order.Character.LocationHex, order.NationId) == null)
            return MakeResult(order, "Must be at one of your population centres to train", false);
        var roll = order.Character?.MageSkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 12;

        if (success)
        {
            order.Character!.MageSkill += 2;
            order.Status = "resolved";
            order.Result = $"Apprentice trained (roll {roll}): +2 mage skill";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"Apprentice training failed (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }


    private object ProcessCastLoreSpell(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        if (!KnowsSpell(order.Character, SpellType.Lore))
            return SpellUnknown(order, SpellType.Lore);

        if (!parameters.TryGetValue("spellId", out var sidEl)
            || SpellCatalog.Get(sidEl.GetInt32()) is not { } def
            || def.Type != SpellType.Lore || def.IsLost
            || !order.Character!.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, "Info must contain a valid known lore spell (spellId)", false);

        var roll = order.Character!.MageSkill + _rng.Next(1, 7);
        var success = roll >= 10;

        if (!success)
        {
            order.Status = "resolved";
            order.Result = $"Lore spell failed (roll {roll})";
            return MakeResult(order, order.Result);
        }

        order.Character.MageSkill = Math.Min(100, order.Character.MageSkill + 1);

        // Cada hechizo mira un tipo de diana (el formulario solo ofrece la suya).
        var kind = def.Id switch
        {
            408 or 420 or 422 or 424 or 430 or 436 => "char",
            406 or 417 or 426 => "commander",
            404 or 419 => "nation",
            432 => "secrets",
            402 or 410 => "allegiance",
            412 or 418 or 428 => "artifact",
            _ => "hex"
        };

        if (kind == "char" && parameters.TryGetValue("targetId", out var tgtEl))
        {
            var tgt = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == tgtEl.GetString());
            if (tgt == null) return MakeResult(order, "Target character not found", false);
            order.Status = "resolved";
            order.Result = $"Scry reveals {tgt.Name} ({tgt.Type}) of {tgt.Nation?.Name ?? "?"} at {tgt.LocationHex}, health {tgt.Health}, challenge {tgt.ChallengeRank}";
            return MakeResult(order, order.Result);
        }
        if (kind == "nation" && parameters.TryGetValue("nationId", out var natEl))
        {
            var nat = game.Nations.FirstOrDefault(n => n.Id == natEl.GetString());
            if (nat == null) return MakeResult(order, "Nation not found", false);
            order.Status = "resolved";
            order.Result = $"Scry reveals {nat.Name}: {nat.PopulationCentres.Count} centres, " +
                $"{nat.Armies.Sum(a => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry + a.LightInfantry + a.Archers + a.MenAtArms)} troops, " +
                $"{nat.Characters.Count(c => !c.IsDead)} characters, gold {nat.Gold}";
            return MakeResult(order, order.Result);
        }
        if (kind == "artifact" && parameters.TryGetValue("artifactId", out var artEl))
        {
            var art = _db.Artifacts.Find(artEl.GetString());
            if (art == null) return MakeResult(order, "Artifact not found", false);
            var holder = art.HeldByCharacterId == null ? null :
                game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == art.HeldByCharacterId);
            order.Status = "resolved";
            order.Result = holder != null
                ? $"Scry locates {art.Name} held by {holder.Name} ({holder.Nation?.Name}) at {holder.LocationHex}"
                : $"Scry locates {art.Name} at {art.LocationHex ?? "unknown"}";
            return MakeResult(order, order.Result);
        }

        if (kind == "commander" && parameters.TryGetValue("commanderId", out var cmdEl))
        {
            var boss = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == cmdEl.GetString());
            if (boss == null || boss.IsDead) return MakeResult(order, "Commander not found", false);
            var forceArmy = game.Nations.SelectMany(n => n.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
            var forceNavy = forceArmy == null
                ? game.Nations.SelectMany(n => n.Navies).FirstOrDefault(v => v.CommanderId == boss.Id)
                : null;
            if (forceArmy == null && forceNavy == null)
                return MakeResult(order, $"{boss.Name} commands no force", false);
            order.Status = "resolved";
            if (def.Id == 417)
            {
                var members = forceArmy != null
                    ? game.Nations.SelectMany(n => n.Characters).Where(c => c.ArmyId == forceArmy.Id && !c.IsDead).Select(c => c.Name).ToList()
                    : new List<string> { boss.Name };
                order.Result = $"Scry reveals travelling with {boss.Name}: {string.Join(", ", members)}";
            }
            else
            {
                var forceHex = forceArmy?.LocationHex ?? forceNavy!.LocationHex;
                var fn = game.Nations.FirstOrDefault(n => n.Id == (forceArmy?.NationId ?? forceNavy!.NationId))?.Name ?? "?";
                order.Result = $"Scry reveals {boss.Name}'s force ({fn}) at {forceHex}";
            }
            return MakeResult(order, order.Result);
        }
        if (kind == "allegiance" && parameters.TryGetValue("allegiance", out var alEl))
        {
            var al = (alEl.GetString() ?? "").ToLower();
            if (al != "free_peoples" && al != "dark_servants" && al != "neutral")
                return MakeResult(order, "Allegiance must be free_peoples, dark_servants or neutral", false);
            var found = game.Nations.Where(n => n.Allegiance == al)
                .Select(n => $"{n.Name} ({n.PopulationCentres.Count} centres, capital {n.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex ?? "?"})")
                .ToList();
            order.Status = "resolved";
            order.Result = found.Count > 0
                ? $"Scry reveals {al} nations: {string.Join("; ", found)}"
                : $"Scry reveals no {al} nations";
            return MakeResult(order, order.Result);
        }
        if (kind == "secrets" && parameters.TryGetValue("nationId", out var secEl))
        {
            var nat = game.Nations.FirstOrDefault(n => n.Id == secEl.GetString());
            if (nat == null) return MakeResult(order, "Nation not found", false);
            var cap = nat.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex ?? "?";
            var status = nat.IsEliminated ? "eliminated" : "active";
            order.Status = "resolved";
            order.Result = $"Scry reveals {nat.Name}: capital at {cap}, victory points {nat.VictoryPoints}, status {status}, {nat.Characters.Count(c => !c.IsDead)} characters";
            return MakeResult(order, order.Result);
        }

        // Revelar ejércitos enemigos en el hex indicado (o el del lanzador)
        var hex = order.Character.LocationHex;
        if (parameters.TryGetValue("hex", out var hexEl)) hex = hexEl.GetString() ?? hex;

        var seen = game.Nations
            .Where(n => n.Id != order.NationId)
            .SelectMany(n => n.Armies)
            .Where(a => a.LocationHex == hex)
            .Select(a => $"{a.Name} ({a.Nation.Name})")
            .ToList();

        order.Status = "resolved";
        order.Result = seen.Count > 0
            ? $"Scry reveals at {hex}: {string.Join(", ", seen)}"
            : $"Scry at {hex}: no enemy armies";
        return MakeResult(order, order.Result);
    }


    private object ProcessCastCombatSpell(Order order, Dictionary<string, JsonElement> parameters)
    {
        // El hechizo de combate se aplica durante la batalla de la fuerza del personaje
        // (orden 230/235/250/255 o un ataque naval), sumando/restando fuerza según la
        // tabla de hechizos de combate. Aquí sólo se registra y resuelve la orden.
        if (order.Character == null || order.Character.MageSkill <= 0)
            return MakeResult(order, "Needs a mage to cast combat spells", false);
        if (!parameters.TryGetValue("spellId", out var sidEl)
            || SpellCatalog.Get(sidEl.GetInt32()) is not { } def
            || def.Type != SpellType.Combat || def.IsLost
            || !order.Character.Spells.Any(s => s.SpellId == def.Id && s.IsKnown && !s.IsLost))
            return MakeResult(order, "Info must contain a valid known combat spell (spellId)", false);
        order.Character.MageSkill = Math.Min(100, order.Character.MageSkill + 1);
        order.Status = "resolved";
        order.Result = $"Combat spell {def.Name} will be cast in battle (+1 mage skill)";
        return MakeResult(order, order.Result);
    }


    // â”€â”€ HELPERS DE HECHIZOS â”€â”€

    private bool KnowsSpell(Character? c, SpellType type)
    {
        if (c == null) return false;
        return c.Spells.Any(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == type);
    }


    private object SpellUnknown(Order order, SpellType type)
    {
        order.Status = "failed";
        order.Result = $"Spell not known: need a {type} spell researched first";
        return MakeResult(order, order.Result, false);
    }
}
