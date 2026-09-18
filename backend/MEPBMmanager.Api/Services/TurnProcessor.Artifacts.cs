using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Finding, moving and wielding artifacts.</summary>
public sealed partial class TurnProcessor
{

    // Tipos de artefacto que cuentan como arma de combate (205). Los tipos
    // protectores (Armor/Shield/Helm/...) y trinkets no valen para combatir.
    public static readonly HashSet<string> CombatArtifactTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sword", "Weapon", "Bow", "Mace", "Scimitar", "Hammer", "Lance",
        "Club", "Flail", "Axe", "Bola", "Spear"
    };

    public static readonly HashSet<string> MovementArtifactTypes = new(StringComparer.OrdinalIgnoreCase) { "Boots" };

    public static readonly HashSet<string> ScryingArtifactTypes = new(StringComparer.OrdinalIgnoreCase) { "Mirror", "Orb", "Sphere" };

    public static readonly HashSet<string> HidingArtifactTypes = new(StringComparer.OrdinalIgnoreCase) { "Cloak", "Robes" };


    // â”€â”€ ARTIFACTS â”€â”€

    private object ProcessUseCombatArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "Missing artifactId: choose a held combat artifact", false);
        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null || artifact.HeldByCharacterId != order.Character?.Id)
            return MakeResult(order, "Artifact not held by character", false);
        if (!CombatArtifactTypes.Contains(artifact.Type ?? ""))
            return MakeResult(order, $"{artifact.Name} is not a combat artifact", false);
        if (!ArtifactUsableBy(artifact, order.Nation))
            return MakeResult(order, $"{artifact.Name} alignment does not match your allegiance", false);

        order.Character!.CommandSkill += artifact.Bonus;
        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: +{artifact.Bonus} command";
        return MakeResult(order, order.Result);
    }


    private object ProcessTransferArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "Missing targetId", false);
        var ids = IdList(parameters, "artifactId");
        if (ids.Count == 0)
            return MakeResult(order, "Missing artifactId(s)", false);

        var targetId = tgtEl.GetString();
        var target = _db.Characters.Find(targetId);
        if (target == null || target.IsKidnapped)
            return MakeResult(order, "Target not found or is a hostage", false);
        if (target.LocationHex != order.Character?.LocationHex)
            return MakeResult(order, "Both characters must be at the same location", false);

        var moved = new List<string>();
        foreach (var artifactId in ids)
        {
            var artifact = _db.Artifacts.Find(artifactId);
            if (artifact == null) continue;
            artifact.HeldByCharacterId = targetId;
            moved.Add(artifact.Name);
        }
        if (moved.Count == 0) return MakeResult(order, "No artifacts transferred", false);
        order.Status = "resolved";
        order.Result = $"Transferred {string.Join(", ", moved)} to {target.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessDropArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ids = IdList(parameters, "artifactId");
        if (ids.Count == 0 || ids.Count > 6)
            return MakeResult(order, "Give 1-6 artifactId(s)", false);

        var dropped = new List<string>();
        foreach (var artifactId in ids)
        {
            var artifact = _db.Artifacts.Find(artifactId);
            if (artifact == null) continue;
            artifact.HeldByCharacterId = null;
            artifact.LocationHex = order.Character?.LocationHex;
            dropped.Add(artifact.Name);
        }
        if (dropped.Count == 0) return MakeResult(order, "No artifacts dropped", false);
        order.Status = "resolved";
        order.Result = $"Dropped {string.Join(", ", dropped)}";
        return MakeResult(order, order.Result);
    }


    private object ProcessPickUpArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var ids = IdList(parameters, "artifactId");
        if (ids.Count == 0 || ids.Count > 6)
            return MakeResult(order, "Give 1-6 artifactId(s)", false);

        var picked = new List<string>();
        foreach (var artifactId in ids)
        {
            var artifact = _db.Artifacts.Find(artifactId);
            if (artifact == null) continue;
            if (order.Character != null)
                artifact.HeldByCharacterId = order.Character.Id;
            picked.Add(artifact.Name);
        }
        if (picked.Count == 0) return MakeResult(order, "No artifacts picked up", false);
        order.Status = "resolved";
        order.Result = $"Picked up {string.Join(", ", picked)}";
        return MakeResult(order, order.Result);
    }


    private object ProcessUseMovementArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "Missing artifactId: choose a held movement artifact", false);
        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null || artifact.HeldByCharacterId != order.Character?.Id)
            return MakeResult(order, "Artifact not held by character", false);
        if (!MovementArtifactTypes.Contains(artifact.Type ?? ""))
            return MakeResult(order, $"{artifact.Name} is not a movement artifact", false);
        if (!ArtifactUsableBy(artifact, order.Nation))
            return MakeResult(order, $"{artifact.Name} alignment does not match your allegiance", false);

        if (parameters.TryGetValue("destination", out var destEl))
        {
            var dest = destEl.GetString();
            if (order.Army != null) order.Army.LocationHex = dest!;
            if (order.Character != null) order.Character.LocationHex = dest!;
        }

        order.Status = "resolved";
        order.Result = $"Used {artifact.Name} for movement";
        return MakeResult(order, order.Result);
    }


    private object ProcessFindArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        var roll = order.Character?.AgentSkill + _rng.Next(1, 7) ?? 8;
        var success = roll >= 14;

        if (success)
        {
            var artifact = new Artifact
            {
                Id = Guid.NewGuid().ToString(),
                NationId = order.NationId,
                Name = "Found Artifact",
                Type = "misc",
                Bonus = _rng.Next(1, 4),
                LocationHex = order.Character?.LocationHex,
                HeldByCharacterId = order.Character?.Id
            };
            _db.Artifacts.Add(artifact);
            order.Status = "resolved";
            order.Result = $"Found artifact (roll {roll})! Bonus +{artifact.Bonus}";
        }
        else
        {
            order.Status = "resolved";
            order.Result = $"No artifact found (roll {roll})";
        }

        return MakeResult(order, order.Result);
    }


    private object ProcessUseScryingArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "Missing artifactId: choose a held scrying artifact", false);
        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null || artifact.HeldByCharacterId != order.Character?.Id)
            return MakeResult(order, "Artifact not held by character", false);
        if (!ScryingArtifactTypes.Contains(artifact.Type ?? ""))
            return MakeResult(order, $"{artifact.Name} is not a scrying artifact", false);
        if (!ArtifactUsableBy(artifact, order.Nation))
            return MakeResult(order, $"{artifact.Name} alignment does not match your allegiance", false);

        var hex = parameters.TryGetValue("hex", out var hEl) ? hEl.GetString() : order.Character?.LocationHex;
        var scryHex = parameters.TryGetValue("hex", out var sHex) ? sHex.GetString() : order.Character?.LocationHex;
        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: scrying {scryHex} active";
        return MakeResult(order, order.Result);
    }


    private object ProcessUseHidingArtifact(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (!parameters.TryGetValue("artifactId", out var artEl))
            return MakeResult(order, "Missing artifactId: choose a held hiding artifact", false);
        var artifact = _db.Artifacts.Find(artEl.GetString());
        if (artifact == null || artifact.HeldByCharacterId != order.Character?.Id)
            return MakeResult(order, "Artifact not held by character", false);
        if (!HidingArtifactTypes.Contains(artifact.Type ?? ""))
            return MakeResult(order, $"{artifact.Name} is not a hiding artifact", false);
        if (!ArtifactUsableBy(artifact, order.Nation))
            return MakeResult(order, $"{artifact.Name} alignment does not match your allegiance", false);

        if (order.Character != null)
            order.Character.Stealth += 10;

        order.Status = "resolved";
        order.Result = $"Used {artifact.Name}: +10 stealth";
        return MakeResult(order, order.Result);
    }


    private object ProcessOneRing(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (order.Nation.Allegiance == "neutral")
            return MakeResult(order, "Neutral nations cannot wield the One Ring", false);
        if (order.Character?.ArmyId != null || order.Character?.CompanyId != null
            || (order.Character != null && _db.Navies.Any(v => v.CommanderId == order.Character.Id)))
            return MakeResult(order, "Bearer must travel alone (no army, company or navy)", false);
        if ((order.Character?.LocationHex ?? "") != "34,23")
            return MakeResult(order, "The bearer must be at Mount Doom (34,23)", false);
        var bearerArt = order.Character == null ? null :
            _db.Artifacts.FirstOrDefault(a => a.HeldByCharacterId == order.Character.Id
                && (a.Id == "14" || (a.Name ?? "").Contains("One Ring", StringComparison.OrdinalIgnoreCase)));
        if (bearerArt == null)
            return MakeResult(order, "Bearer must possess artifact #14 The One Ring", false);
        var roll = _rng.Next(1, 7);
        var success = roll >= 6;
        order.Status = "resolved";
        order.Result = success
            ? $"The One Ring obeys! (roll {roll})"
            : $"The One Ring resists... (roll {roll})";
        return MakeResult(order, order.Result);
    }
}
