using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

/// <summary>Population centres, structures and camps.</summary>
public sealed partial class TurnProcessor
{
    private readonly HashSet<string> _fortifiedThisTurn = new();


    // Nombres de campamento por nación (552/555 cuando no se indica nombre).
    private static readonly Dictionary<string, string[]> CampNamePools = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Woodmen", new[] { "Buhr Widu", "Eryn Camp", "Carrock Outpost" } },
        { "Northmen", new[] { "Dale Camp", "Esgaroth Camp", "Dorwinion Camp" } },
        { "Riders of Rohan", new[] { "Westfold Camp", "Eastemnet Camp", "Fenmarch Camp" } },
        { "Dúnadan Rangers", new[] { "Esteldin Camp", "Fornost Camp", "Weather Camp" } },
        { "Silvan Elves", new[] { "Galabryn Camp", "Eryn Camp", "Celeb Camp" } },
        { "Northern Gondor", new[] { "Ithilien Camp", "Anorien Camp", "Lossarnach Camp" } },
        { "Southern Gondor", new[] { "Lebennin Camp", "Belfalas Camp", "Lamedon Camp" } },
        { "Dwarves", new[] { "Khazad Camp", "Baraz Camp", "Zirak Camp" } },
        { "Sinda Elves", new[] { "Lothlorien Camp", "Ceryn Camp", "Nimrodel Camp" } },
        { "Noldo Elves", new[] { "Elost Camp", "Forlindon Camp", "Mithlond Camp" } },
        { "Witch-king", new[] { "Angmar Camp", "Carn Dum Camp", "Morgul Camp" } },
        { "Dragon Lord", new[] { "Withered Camp", "Ered Camp", "Guldur Camp" } },
        { "Dog Lord", new[] { "Nurn Camp", "Morannon Camp", "Udun Camp" } },
        { "Cloud Lord", new[] { "Ered Lithui Camp", "Nargil Camp", "Mornen Camp" } },
        { "Blind Sorcerer", new[] { "Nurnen Camp", "Ulgath Camp", "Lugur Camp" } },
        { "Ice King", new[] { "Forochel Camp", "Lossoth Camp", "Helcaraxe Camp" } },
        { "Quiet Avenger", new[] { "Harad Camp", "Harnen Camp", "Korondor Camp" } },
        { "Fire King", new[] { "Orodruin Camp", "Gorgoroth Camp", "Udun Camp" } },
        { "Long Rider", new[] { "Steppe Camp", "Khand Camp", "Variag Camp" } },
        { "Dark Lieutenants", new[] { "Barad Camp", "Lugburz Camp", "Durburz Camp" } },
        { "Corsairs", new[] { "Umbar Camp", "Havens Camp", "Belfalas Camp" } },
        { "Dunlendings", new[] { "Dunland Camp", "Enedwaith Camp", "Isen Camp" } },
        { "Khand Easterlings", new[] { "Khand Camp", "Variag Camp", "Easterling Camp" } },
        { "Rhûn Easterlings", new[] { "Rhun Camp", "Dorwinion Camp", "Sea Camp" } },
        { "White Wizard", new[] { "Isengard Camp", "Fangorn Camp", "Wizard Camp" } },
    };

    private static readonly string[] GenericCampNames = { "Camp", "Outpost", "Waycamp" };


    private object ProcessImproveHarbour(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        var pc = hex == null ? null : OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at one of your population centres", false);
        if (!pc.HasHarbour || pc.HasPort)
            return MakeResult(order, $"{pc.Name} needs a harbour (not yet a port)", false);
        if (pc.Size != "major town" && pc.Size != "city")
            return MakeResult(order, "Only major towns and cities can have ports", false);
        if (EnemyAtHex(game, hex, order.NationId))
            return MakeResult(order, "Enemy forces present", false);
        // Derivado de la tabla: puerto menos puerto (4000/7500 - 2500/5000).
        const int goldCost = 1500;
        const int timberCost = 2500;
        if (order.Nation.Gold < goldCost || order.Nation.Timber < timberCost)
        {
            order.Status = "failed";
            order.Result = $"Insufficient resources: need {goldCost} gold and {timberCost} timber";
            return MakeResult(order, order.Result, false);
        }

        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= timberCost;
        pc.HasPort = true;
        order.Status = "resolved";
        order.Result = $"Harbour at {pc.Name} improved to port for {goldCost} gold and {timberCost} timber";
        return MakeResult(order, order.Result);
    }


    private object ProcessAddHarbour(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at your own population centre", false);
        if (pc.HasHarbour || pc.HasPort)
            return MakeResult(order, $"{pc.Name} already has a harbour or port", false);
        if (pc.Size != "town" && pc.Size != "major town" && pc.Size != "city")
            return MakeResult(order, "Only towns and larger can have harbours", false);
        if (EnemyAtHex(game, hex, order.NationId))
            return MakeResult(order, "Enemy forces present", false);
        // Reglamento: puerto 2500 oro + 5000 madera.
        const int goldCost = 2500;
        const int timberCost = 5000;
        if (order.Nation.Gold < goldCost || order.Nation.Timber < timberCost)
            return MakeResult(order, $"Insufficient resources: need {goldCost} gold and {timberCost} timber", false);
        order.Nation.Gold -= goldCost;
        order.Nation.Timber -= timberCost;
        pc.HasHarbour = true;
        order.Status = "resolved";
        order.Result = $"Harbour added to {pc.Name} for {goldCost} gold and {timberCost} timber";
        return MakeResult(order, order.Result);
    }


    private object ProcessImprovePC(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = OwnedPCAt(hex, order.NationId);
        if (pc == null) return MakeResult(order, "Must be at your own population centre to improve it", false);
        if (pc.Size == "city" || pc.Size == "citadel")
            return MakeResult(order, $"{pc.Name} cannot be improved further", false);
        if (EnemyAtHex(game, hex, order.NationId))
            return MakeResult(order, "Enemy forces present", false);
        // Reglamento: coste de subida según tamaño (camp 2000 … city 10000).
        int cost = pc.Size.ToLower() switch
        {
            "camp" => 2000,
            "village" => 4000,
            "town" => 6000,
            "major town" => 8000,
            _ => 10000
        };
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        var roll = order.Character?.EmissarySkill + _rng.Next(1, 7) ?? 8;
        if (roll < 10) return MakeResult(order, $"Improvement failed (roll {roll})", false);
        order.Nation.Gold -= cost;
        var idx = Array.IndexOf(SizeOrder, pc.Size.ToLower());
        if (idx >= 0 && idx < SizeOrder.Length - 1)
        {
            pc.Size = SizeOrder[idx + 1];
            order.Status = "resolved";
            order.Result = $"Improved {pc.Name} to {pc.Size} (roll {roll})";
        }
        else
        {
            pc.Production += 100;
            order.Status = "resolved";
            order.Result = $"{pc.Name} production increased (roll {roll})";
        }
        return MakeResult(order, order.Result);
    }


    private object ProcessCreateCamp(Order order, Dictionary<string, JsonElement> parameters, Game game)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for camp", false);
        if (!IsLandHex(order.GameId, hex))
            return MakeResult(order, "Camps need a land hex", false);
        if (EnemyAtHex(game, hex, order.NationId))
            return MakeResult(order, "Enemy forces present", false);
        // Reglamento: crear campamento 2000 oro.
        const int cost = 2000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        if (_db.PopulationCentres.Any(p => p.LocationHex == hex && p.NationId == order.NationId))
            return MakeResult(order, "Already have a population centre at this hex", false);
        order.Nation.Gold -= cost;
        var camp = new PopulationCentre
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = CampName(order, parameters),
            Size = "camp",
            LocationHex = hex,
            Loyalty = 50,
            Production = 100,
            Stores = 0
        };
        _db.PopulationCentres.Add(camp);
        order.Status = "resolved";
        order.Result = $"Camp {camp.Name} created at {hex} for {cost} gold";
        return MakeResult(order, order.Result);
    }


    // Nombre indicado o pool acorde a la nación.
    private string CampName(Order order, Dictionary<string, JsonElement> parameters)
    {
        if (parameters.TryGetValue("name", out var nEl))
        {
            var given = (nEl.GetString() ?? "").Trim();
            if (given.Length > 0) return given[..Math.Min(60, given.Length)];
        }
        var pool = CampNamePools.TryGetValue(order.Nation?.Name ?? "", out var names) ? names : GenericCampNames;
        return pool[_rng.Next(pool.Length)];
    }


    public static string[] CampSuggestions(string? nationName)
    {
        if (nationName != null && CampNamePools.TryGetValue(nationName, out var names)) return names;
        return GenericCampNames;
    }


    private object ProcessAbandonCamp(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var camp = _db.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex && p.NationId == order.NationId && p.Size == "camp");
        if (camp == null) return MakeResult(order, "No camp to abandon at this hex", false);
        _db.PopulationCentres.Remove(camp);
        order.Status = "resolved";
        order.Result = $"Camp at {hex} abandoned";
        return MakeResult(order, order.Result);
    }


    private object ProcessReducePC(Order order, Dictionary<string, JsonElement> parameters)
    {
        var hex = parameters.TryGetValue("hex", out var h) ? h.GetString()
            : (order.Character?.LocationHex ?? order.Army?.LocationHex);
        if (hex == null) return MakeResult(order, "No location", false);
        var pc = PCAtHex(hex);
        if (pc == null) return MakeResult(order, "No population centre at location", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only reduce your own population centres", false);
        if (pc.Size.ToLower() == "camp")
            return MakeResult(order, "Camps cannot be reduced further (abandon them)", false);
        var idx = Array.IndexOf(SizeOrder, pc.Size.ToLower());
        if (idx > 0) pc.Size = SizeOrder[idx - 1];
        pc.Loyalty = Math.Max(0, pc.Loyalty - 20);
        pc.Production = Math.Max(0, pc.Production - 50);
        order.Status = "resolved";
        order.Result = $"Reduced {pc.Name} (now {pc.Size}, loyalty {pc.Loyalty})";
        return MakeResult(order, order.Result);
    }


    private object ProcessRemoveHarbour(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only remove your own harbour", false);
        if (!pc.HasHarbour) return MakeResult(order, $"{pc.Name} has no harbour", false);
        pc.HasHarbour = false;
        order.Status = "resolved"; order.Result = $"Harbour removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessRemovePort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only remove your own port", false);
        if (!pc.HasPort) return MakeResult(order, $"{pc.Name} has no port", false);
        pc.HasPort = false;
        order.Status = "resolved"; order.Result = $"Port removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessDestroyStores(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : pc.Stores;
        pc.Stores = Math.Max(0, pc.Stores - amt);
        order.Status = "resolved"; order.Result = $"Destroyed {amt} stores at {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessRemoveFort(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        pc.Fortification = null;
        order.Status = "resolved"; order.Result = $"Fortifications removed from {pc.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessFortifyPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only fortify your own population centres", false);
        if ((pc.Fortification ?? "").Contains("itadel", StringComparison.OrdinalIgnoreCase))
            return MakeResult(order, $"{pc.Name} already has citadel-class fortifications", false);
        if (_fortifiedThisTurn.Contains(pc.Id))
            return MakeResult(order, $"{pc.Name} was already fortified this turn", false);
        if (order.Character == null) return MakeResult(order, "No character", false);
        int roll = order.Character.CommandSkill + _rng.Next(1, 7);
        if (roll < 12)
            return MakeResult(order, $"Failed to fortify {pc.Name} (roll {roll})", false);
        // Escalera oficial Tower-Fort-Castle-Keep-Citadel: solo sube un nivel
        // por turno (el parámetro 1-5 equivale al tipo; por defecto el siguiente).
        var ladder = new[] { "Tower", "Fort", "Castle", "Keep", "Citadel" };
        int cur = FortLadderIndex(pc.Fortification);
        int want = cur + 1;
        if (p.TryGetValue("level", out var lv)) want = Math.Clamp(lv.GetInt32(), 1, 5) - 1;
        if (want != cur + 1 || want < 0 || want > 4)
            return MakeResult(order, $"Must build exactly one level (next: {(cur + 1 <= 4 ? ladder[cur + 1] : "none")})", false);
        var fort = ladder[want];
        // Costes D-2. Madera de stores del PC.
        int timber = NationAbilities.FortTimberCost(order.Nation?.Name, fort ?? "palisade");
        int gold = NationAbilities.FortGoldCost(fort ?? "palisade");
        if (pc.Stores < timber || order.Nation.Gold < gold)
            return MakeResult(order, $"Insufficient stores: need {timber} timber and {gold} gold", false);
        pc.Stores -= timber;
        order.Nation.Gold -= gold;
        pc.Fortification = fort;
        _fortifiedThisTurn.Add(pc.Id);
        order.Character.CommandSkill = Math.Min(100, order.Character.CommandSkill + _rng.Next(1, 6));
        order.Status = "resolved"; order.Result = $"Fortified {pc.Name} to {pc.Fortification} for {timber} timber and {gold} gold (roll {roll})";
        return MakeResult(order, order.Result);
    }


    private object ProcessThreatenPC(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId == order.NationId) return MakeResult(order, "Cannot threaten your own centre (use 520)", false);
        if (pc.IsHidden) return MakeResult(order, "No visible population centre here", false);
        // Solo naciones desagradas/odiadas (nivel <= -1 hacia ellas).
        var rel = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == pc.NationId);
        if (rel == null || rel.Level > -1)
            return MakeResult(order, "Can only threaten disliked or hated nations", false);
        // Solo comandante de ejército/armada.
        bool commands = order.Character?.ArmyId != null || order.Army != null
            || game.Nations.SelectMany(n => n.Navies).Any(v => v.CommanderId == order.Character?.Id);
        if (!commands) return MakeResult(order, "Only an army or navy commander can threaten", false);
        // Sin enemigos presentes (los que nos consideran enemigos).
        bool enemyPresent = game.Nations
            .SelectMany(n => n.Armies.Select(a => new { a.LocationHex, NationId = n.Id })
                .Concat(n.Navies.Select(v => new { v.LocationHex, NationId = n.Id })))
            .Any(u => u.LocationHex == pc.LocationHex && u.NationId != order.NationId
                && game.Nations.FirstOrDefault(n => n.Id == u.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == order.NationId)?.Level <= -1);
        if (enemyPresent) return MakeResult(order, "Enemy forces present", false);
        var amt = p.TryGetValue("amount", out var a) ? Math.Max(0, a.GetInt32()) : 10;
        pc.Loyalty = Math.Max(0, pc.Loyalty - amt);
        order.Status = "resolved"; order.Result = $"Threatened {pc.Name}, loyalty down to {pc.Loyalty}";
        return MakeResult(order, order.Result);
    }


    private object ProcessTransferOwnership(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        if (order.Character == null) return MakeResult(order, "No character", false);
        if (order.Character.EmissarySkill <= 0)
            return MakeResult(order, "Needs emissary skill", false);
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "Can only transfer your own population centres", false);
        if (pc.IsHidden) return MakeResult(order, "Population centre is hidden", false);
        if (pc.IsCapital) return MakeResult(order, "Cannot transfer the capital", false);
        if (pc.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Must be at the location of the centre", false);
        if (!p.TryGetValue("targetId", out var tgtEl))
            return MakeResult(order, "Missing targetId: emissary receiving the centre", false);
        var target = game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == tgtEl.GetString());
        if (target == null || target.IsDead) return MakeResult(order, "Target not found", false);
        if (target.IsKidnapped) return MakeResult(order, "Target is a hostage", false);
        if (target.EmissarySkill <= 0) return MakeResult(order, "Target needs emissary skill", false);
        if (target.LocationHex != order.Character.LocationHex)
            return MakeResult(order, "Target must be at the same location", false);
        if (target.NationId == order.NationId)
            return MakeResult(order, "Target must be of another nation", false);
        var targetNation = game.Nations.FirstOrDefault(n => n.Id == target.NationId);
        if (targetNation == null) return MakeResult(order, "Target nation not found", false);
        var fwd = order.Nation.Relations.FirstOrDefault(r => r.TargetNationId == target.NationId);
        if ((fwd?.Level ?? 0) <= -1) return MakeResult(order, "Nations are enemies", false);
        pc.NationId = target.NationId;
        order.Status = "resolved";
        order.Result = $"Ownership of {pc.Name} transferred to {targetNation.Name} via {target.Name}";
        return MakeResult(order, order.Result);
    }


    private object ProcessRelocateCapital(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var pc = ResolvePC(order, p, game);
        if (pc == null) return MakeResult(order, "No population centre", false);
        if (order.Character == null || order.Character.LocationHex != (CapitalHex(order.Nation) ?? ""))
            return MakeResult(order, "Must be at your current capital", false);
        if (pc.NationId != order.NationId)
            return MakeResult(order, "New capital must be owned by your nation", false);
        if (pc.IsSieged) return MakeResult(order, "Capital and new capital must not be under siege", false);
        var curCap = order.Nation.PopulationCentres.FirstOrDefault(x => x.IsCapital);
        if (curCap != null && curCap.IsSieged)
            return MakeResult(order, "Capital and new capital must not be under siege", false);
        if (pc.Size != "major town" && pc.Size != "city")
            return MakeResult(order, "New capital must be a major town or city", false);
        // Reglamento: 25000 oro.
        const int cost = 25000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        order.Nation.Gold -= cost;
        foreach (var c in order.Nation.PopulationCentres) c.IsCapital = false;
        pc.IsCapital = true;
        order.Status = "resolved"; order.Result = $"Capital relocated to {pc.Name} for {cost} gold";
        return MakeResult(order, order.Result);
    }


    private static int RankParam(Dictionary<string, JsonElement> p, string key, int def)
    {
        if (p.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number)
            return Math.Clamp(el.GetInt32(), 0, 30);
        return def;
    }


    private object ProcessDestroyBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: personaje con mando; si va suelto exige PC propia en el hex
        // (el comandante de ejercito/armada no la necesita). Exito por rango de mando.
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasBridge) return MakeResult(order, $"No bridge at {hex}", false);
        bool commands = ch.ArmyId != null || _db.Navies.Any(n => n.CommanderId == ch.Id);
        if (!commands && !_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == ch.NationId))
            return MakeResult(order, "Destroying a bridge alone requires one of your population centres in the hex", false);
        int roll = ch.CommandSkill + _rng.Next(1, 7);
        if (roll < 12)
            return MakeResult(order, $"Failed to destroy the bridge at {hex} (roll {roll})", false);
        tile.HasBridge = false;
        order.Status = "resolved"; order.Result = $"Bridge at {hex} destroyed (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessBuildBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: personaje con mando (+PC propia en el hex salvo comandante);
        // menor: 5000 madera + 2500 oro; mayor: 10000 madera + 5000 oro;
        // el mayor exige camino (une las dos mitades); sin vado/puente previo.
        // Exito por rango de mando (madera/oro solo se cobran si sale).
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasMajorRiver && !tile.HasMinorRiver)
            return MakeResult(order, $"No river at {hex}", false);
        if (tile.HasBridge || tile.HasFord)
            return MakeResult(order, $"A ford or bridge already exists at {hex}", false);
        bool major = tile.HasMajorRiver;
        if (major && !tile.HasRoad)
            return MakeResult(order, $"A bridge over a major river at {hex} requires a road", false);
        int timber = major ? 10000 : 5000;
        int gold = major ? 5000 : 2500;
        if (order.Nation.Timber < timber || order.Nation.Gold < gold)
            return MakeResult(order, $"Insufficient stores: need {timber} timber and {gold} gold", false);
        bool commands = ch.ArmyId != null || _db.Navies.Any(n => n.CommanderId == ch.Id);
        if (!commands && !_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == ch.NationId))
            return MakeResult(order, "Building a bridge alone requires one of your population centres in the hex", false);
        int roll = ch.CommandSkill + _rng.Next(1, 7);
        int need = major ? 18 : 12;
        if (roll < need)
            return MakeResult(order, $"Failed to build the bridge at {hex} (roll {roll} vs {need})", false);
        order.Nation.Timber -= timber;
        order.Nation.Gold -= gold;
        tile.HasBridge = true;
        order.Status = "resolved"; order.Result = $"Bridge built at {hex} for {timber} timber and {gold} gold (roll {roll})";
        return MakeResult(order, order.Result);
    }

    private object ProcessSabotageBridge(Order order, Dictionary<string, JsonElement> p)
    {
        // Reglamento: agente; se opone el mejor guardian del hex (orden 605),
        // sin guardian vale dificultad fija. Exito por rango de agente.
        var ch = order.Character;
        if (ch == null) return MakeResult(order, "No character", false);
        var hex = p.TryGetValue("hex", out var h) ? h.GetString() : ch.LocationHex;
        if (hex == null) return MakeResult(order, "No location", false);
        var tile = TileAt(order.Nation.GameId, hex);
        if (tile == null) return MakeResult(order, "No such hex", false);
        if (!tile.HasBridge) return MakeResult(order, $"No bridge at {hex}", false);
        var guardIds = _db.Guards.Where(g => g.TargetId == hex).Select(g => g.CharacterId).ToList();
        var guards = _db.Characters.Where(c => guardIds.Contains(c.Id) && !c.IsDead).ToList();
        int defense = guards.Count > 0 ? guards.Max(g => g.AgentSkill) + _rng.Next(1, 7) : 14;
        int roll = ch.AgentSkill + _rng.Next(1, 7);
        if (roll < defense)
            return MakeResult(order, $"Bridge sabotage at {hex} thwarted (roll {roll} vs {defense})", false);
        tile.HasBridge = false;
        order.Status = "resolved"; order.Result = $"Bridge at {hex} sabotaged (roll {roll} vs {defense})";
        return MakeResult(order, order.Result);
    }

    private object ProcessPostCamp(Order order, Dictionary<string, JsonElement> p, Game game)
    {
        var hex = order.Character?.LocationHex ?? order.Army?.LocationHex;
        if (hex == null) return MakeResult(order, "No location for camp", false);
        if (!IsLandHex(order.GameId, hex))
            return MakeResult(order, "Camps need a land hex", false);
        if (EnemyAtHex(game, hex, order.NationId))
            return MakeResult(order, "Enemy forces present", false);
        // Reglamento: asentar campamento 4000 oro.
        const int cost = 4000;
        if (order.Nation.Gold < cost) return MakeResult(order, $"Insufficient gold: need {cost}", false);
        if (_db.PopulationCentres.Any(pc => pc.LocationHex == hex && pc.NationId == order.NationId))
            return MakeResult(order, "Already have a population centre at this hex", false);
        order.Nation.Gold -= cost;
        var camp = new PopulationCentre
        {
            Id = Guid.NewGuid().ToString(),
            NationId = order.NationId,
            Name = CampName(order, p),
            Size = "camp",
            LocationHex = hex,
            Loyalty = 50,
            Production = 100,
            Stores = 0
        };
        _db.PopulationCentres.Add(camp);
        order.Status = "resolved";
        order.Result = $"Camp {camp.Name} posted at {hex} for {cost} gold";
        return MakeResult(order, order.Result);
    }
}
