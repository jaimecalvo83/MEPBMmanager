using System.Text.Json;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Orders;

/// <summary>
/// Order rules: eligibility, per-order forms (<see cref="RequiresFor"/>),
/// validation, cross-order conflicts and live cost estimates.
/// No HTTP here: the controller only maps requests/responses.
/// All user-facing strings live in <see cref="OrderTexts"/>.
/// </summary>
public sealed partial class OrderEstimateService
{
    private readonly MepbmDbContext _db;

    public OrderEstimateService(MepbmDbContext db)
    {
        _db = db;
    }

    private static readonly HashSet<int> MoveCodes = new() { 810, 820, 830, 850, 860, 870, 825 };

    private static readonly (string Short, string Full)[] TroopCodes =
    {
        ("hc", "HeavyCavalry"), ("lc", "LightCavalry"), ("hi", "HeavyInfantry"),
        ("li", "LightInfantry"), ("ar", "Archers"), ("ma", "MenAtArms"),
    };

    private static Dictionary<string, System.Text.Json.JsonElement> ParseEstimateParams(System.Text.Json.JsonElement? el)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (el.HasValue && el.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var p in el.Value.EnumerateObject()) d[p.Name] = p.Value;
        return d;
    }

    /// <summary>Full estimate pipeline for one order (form + errors + costs).</summary>
    public async Task<object> EstimateAsync(string gameId, EstimateOrderRequest req, string lang, string nationId)
    {
        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == req.CharacterId && c.NationId == nationId);
        if (ch == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.character-not-found"));

        var game = await _db.Games
            .AsSplitQuery()
            .Include(g => g.Nations).ThenInclude(n => n.Characters)
            .Include(g => g.Nations).ThenInclude(n => n.Armies)
            .Include(g => g.Nations).ThenInclude(n => n.Navies)
            .Include(g => g.Nations).ThenInclude(n => n.PopulationCentres)
            .Include(g => g.Nations).ThenInclude(n => n.Relations)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.game-not-found"));
        var nation = game.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.nation-not-found"));

        var def = OrderDefinitions.Orders.FirstOrDefault(o => o.Code == req.Code);
        if (def == null) throw new OrderRequestException(400, OrderTexts.Get(lang, "err.invalid-order-code"));

        var parameters = ParseEstimateParams(req.Parameters);
        string? afterDest = null;
        if (req.AfterOrder != null && MoveCodes.Contains(req.AfterOrder.Code))
        {
            var afterParams = ParseEstimateParams(req.AfterOrder.Parameters);
            if (afterParams.TryGetValue("destination", out var destEl) && destEl.ValueKind == System.Text.Json.JsonValueKind.String)
                afterDest = destEl.GetString();
        }
        var effLoc = afterDest ?? ch.LocationHex;

        var commandsNavy = nation.Navies.Any(v => v.CommanderId == ch.Id);
        var (eligible, reason) = CheckEligible(ch, def, nation, commandsNavy, lang);
        if (!eligible)
            return new { ok = false, errors = new[] { reason }, costs = new { }, requires = Array.Empty<object>(),
                effectiveLocation = effLoc, movedByFirstOrder = afterDest != null };

        var ctx = new EstimateCtx(game, nation, ch, effLoc, parameters, lang);
        var scope = new EstimateScope(_db, ctx);
        var requires = RequiresFor(req.Code, ctx);
        ValidateEstimateParams(req.Code, scope, requires);
        scope.Pending = await PendingNationOrders(gameId, nation.Id);
        scope.Used = SummarizePendingUsage(scope.Pending, nation, game);
        var estimate = EstimateCosts(req.Code, scope);
        if (estimate.MaxAmount == 0)
            scope.Errors.Add(OrderTexts.Get(lang, "err.insufficient-resources-or-capacity-to-execute-ma"));
        CrossOrderConflicts(req.Code, scope);

        object? suggestNames = null;
        if (req.Code is 552 or 555)
            suggestNames = TurnProcessor.CampSuggestions(nation.Name);

        return new
        {
            ok = scope.Errors.Count == 0,
            errors = scope.Errors,
            warnings = scope.Warnings,
            costs = estimate.Costs,
            maxAmount = estimate.MaxAmount,
            expectedGold = estimate.ExpectedGold,
            requires,
            effectiveLocation = effLoc,
            movedByFirstOrder = afterDest != null,
            suggestNames
        };
    }

    /// <summary>Eligibility of every order for one character.</summary>
    public async Task<object> EligibleAsync(string gameId, string characterId, string lang, string nationId)
    {
        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.NationId == nationId);
        if (ch == null) throw new OrderRequestException(404, OrderTexts.Get(lang, "err.character-not-found"));

        var nation = await _db.Nations
            .Include(n => n.Armies).Include(n => n.Navies).Include(n => n.PopulationCentres)
            .FirstOrDefaultAsync(n => n.Id == nationId);
        var commandsNavy = nation?.Navies.Any(v => v.CommanderId == ch.Id) == true;

        var list = OrderDefinitions.Orders.Select(d =>
        {
            var (ok, reason) = CheckEligible(ch, d, nation, commandsNavy, lang);
            return new { code = d.Code, ok, reason };
        }).ToList();
        return new { eligible = list };
    }
}

