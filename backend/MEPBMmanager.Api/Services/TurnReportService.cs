using System.Text.Json;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Services;

public class TurnReportService
{
    private readonly MepbmDbContext _db;

    public TurnReportService(MepbmDbContext db)
    {
        _db = db;
    }

    public async Task<object?> GetReportAsync(string gameId, string turnId, string? nationId = null)
    {
        var turn = await _db.Turns
            .Include(t => t.Game).ThenInclude(g => g.Nations)
                .ThenInclude(n => n.Armies)
            .Include(t => t.Game).ThenInclude(g => g.Nations)
                .ThenInclude(n => n.PopulationCentres)
            .Include(t => t.Game).ThenInclude(g => g.Nations)
                .ThenInclude(n => n.Characters)
            .FirstOrDefaultAsync(t => t.Id == turnId && t.GameId == gameId);

        if (turn == null) return null;

        var turnResult = await _db.TurnResults
            .FirstOrDefaultAsync(r => r.TurnId == turnId);

        var allResults = turnResult != null ? ParseResults(turnResult.Content) : new();

        // Filtrar por nación solo para órdenes (economía/hambre se muestran todas)
        if (nationId != null)
        {
            allResults = allResults.Where(r =>
            {
                // Economy/hunger: always show all nations
                if (r.TryGetValue("type", out var t))
                {
                    var type = t.GetString();
                    if (type == "economy" || type == "hunger") return true;
                }
                // Orders: filter by player's nation
                if (r.TryGetValue("nation", out var n))
                {
                    var nationName = n.GetString();
                    var matchingNation = turn.Game.Nations.FirstOrDefault(na => na.Name == nationName);
                    return matchingNation?.Id == nationId;
                }
                return true;
            }).ToList();
        }

        var sections = new List<object>();

        // ── RESUMEN DE NACIÓN (solo para jugadores con nación asignada) ──
        if (nationId != null)
        {
            var nation = turn.Game.Nations.FirstOrDefault(n => n.Id == nationId);
            if (nation != null)
            {
                sections.Add(new
                {
                    title = "Nation Summary",
                    entries = new object[]
                    {
                        new { category = "Resources", detail = $"Gold: {nation.Gold}, Food: {nation.Food}, Timber: {nation.Timber}, Leather: {nation.Leather}, Bronze: {nation.Bronze}, Steel: {nation.Steel}, Mithril: {nation.Mithril}, Mounts: {nation.Mounts}" },
                        new { category = "Tax Rate", detail = $"{nation.TaxRate}%" },
                        new { category = "Armies", detail = FormatArmies(nation.Armies) },
                        new { category = "Population Centres", detail = string.Join("; ", nation.PopulationCentres.Select(pc => pc.Name + " (" + pc.Size + ")")) },
                        new { category = "Characters", detail = string.Join("; ", nation.Characters.Select(c => c.Name + " (" + c.Type + ") HP=" + c.Health)) }
                    }
                });
            }
        }
        else
        {
            // Admin: resumen de TODAS las naciones
            foreach (var nation in turn.Game.Nations.OrderBy(n => n.Name))
            {
                sections.Add(new
                {
                    title = $"Nation: {nation.Name} ({nation.Allegiance})",
                    entries = new object[]
                    {
                        new { category = "Resources", detail = $"Gold: {nation.Gold}, Food: {nation.Food}, Timber: {nation.Timber}, Steel: {nation.Steel}, Mounts: {nation.Mounts}" },
                        new { category = "Armies", detail = FormatArmies(nation.Armies) },
                        new { category = "Population Centres", detail = string.Join("; ", nation.PopulationCentres.Select(pc => pc.Name + " (" + pc.Size + ")")) }
                    }
                });
            }
        }

        // ── ECONOMÍA (siempre todas las naciones) ──
        var economyResults = allResults.Where(r => r.TryGetValue("type", out var t) && t.GetString() == "economy").ToList();
        if (economyResults.Any())
        {
            var economyEntries = economyResults.Select(r => new
            {
                nation = r.TryGetValue("nation", out var n) ? n.GetString() : "?",
                gold = r.TryGetValue("gold", out var g) ? g.GetInt32() : 0,
                food = r.TryGetValue("food", out var f) ? f.GetInt32() : 0,
                tax = r.TryGetValue("tax", out var tx) ? tx.GetInt32() : 0,
                message = r.TryGetValue("message", out var m) ? m.GetString() : ""
            }).ToList();
            sections.Add(new { title = "Economy", entries = economyEntries });
        }

        // ── HAMBRE ──
        var hungerResults = allResults.Where(r => r.TryGetValue("type", out var t) && t.GetString() == "hunger").ToList();
        if (hungerResults.Any())
        {
            var hungerEntries = hungerResults.Select(r => new
            {
                nation = r.TryGetValue("nation", out var n) ? n.GetString() : "?",
                army = r.TryGetValue("army", out var a) ? a.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : ""
            }).ToList();
            sections.Add(new { title = "Famine", entries = hungerEntries });
        }

        // ── ÓRDENES POR TIPO ──
        var orderResults = allResults.Where(r => r.ContainsKey("orderId")).ToList();

        // Movimiento (codes 810-870)
        var movementOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && c.GetInt32() >= 810 && c.GetInt32() <= 870).ToList();
        if (movementOrders.Any())
        {
            var entries = movementOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Movement", entries });
        }

        // Combate (codes 230-260)
        var combatOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && c.GetInt32() >= 230 && c.GetInt32() <= 260).ToList();
        if (combatOrders.Any())
        {
            var entries = combatOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Combat", entries });
        }

        // Reclutamiento (codes 400-448)
        var recruitOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && c.GetInt32() >= 400 && c.GetInt32() <= 448).ToList();
        if (recruitOrders.Any())
        {
            var entries = recruitOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Recruitment", entries });
        }

        // Economía/mercado (codes 300-375)
        var econOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && c.GetInt32() >= 300 && c.GetInt32() <= 375).ToList();
        if (econOrders.Any())
        {
            var entries = econOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Economic Orders", entries });
        }

        // Magia (codes 120, 225, 330, 700-710, 825, 940)
        var magicCodes = new[] { 120, 225, 330, 700, 705, 710, 825, 940 };
        var magicOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && magicCodes.Contains(c.GetInt32())).ToList();
        if (magicOrders.Any())
        {
            var entries = magicOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Magic", entries });
        }

        // Otras órdenes
        var handledCodes = new HashSet<int> { 100 };
        foreach (var c in Enumerable.Range(810, 61)) handledCodes.Add(c);
        foreach (var c in Enumerable.Range(230, 31)) handledCodes.Add(c);
        foreach (var c in Enumerable.Range(400, 49)) handledCodes.Add(c);
        foreach (var c in Enumerable.Range(300, 76)) handledCodes.Add(c);
        foreach (var c in magicCodes) handledCodes.Add(c);
        var otherOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && !handledCodes.Contains(c.GetInt32())).ToList();
        if (otherOrders.Any())
        {
            var entries = otherOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : "",
                success = r.TryGetValue("success", out var s) && s.GetBoolean()
            }).ToList();
            sections.Add(new { title = "Other Orders", entries });
        }

        // Hold orders (auto-generated)
        var holdOrders = orderResults.Where(r => r.TryGetValue("code", out var c) && c.GetInt32() == 100).ToList();
        if (holdOrders.Any())
        {
            var entries = holdOrders.Select(r => new
            {
                character = r.TryGetValue("character", out var c) ? c.GetString() : "?",
                message = r.TryGetValue("message", out var m) ? m.GetString() : ""
            }).ToList();
            sections.Add(new { title = "Auto-Hold (Inactive)", entries });
        }

        return new
        {
            turn = new { turn.Number, turn.Season, turn.Status, turn.Deadline, turn.ProcessedAt },
            sections
        };
    }

    private static string FormatArmies(System.Collections.Generic.ICollection<MEPBMmanager.Domain.Entities.Army> armies)
    {
        if (!armies.Any()) return "None";
        return string.Join("; ", armies.Select(a => a.Name + ": HC=" + a.HeavyCavalry + " LC=" + a.LightCavalry + " HI=" + a.HeavyInfantry + " LI=" + a.LightInfantry + " Arch=" + a.Archers + " MAA=" + a.MenAtArms + " Morale=" + a.Morale + " WR=" + a.WeaponRank + " AR=" + a.ArmourRank));
    }

    private static List<Dictionary<string, JsonElement>> ParseResults(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("results", out var resultsArr) && resultsArr.ValueKind == JsonValueKind.Array)
            {
                var list = new List<Dictionary<string, JsonElement>>();
                foreach (var item in resultsArr.EnumerateArray())
                {
                    var dict = new Dictionary<string, JsonElement>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        dict[prop.Name] = prop.Value;
                    }
                    list.Add(dict);
                }
                return list;
            }
        }
        catch { }
        return new List<Dictionary<string, JsonElement>>();
    }
}
