using System.Security.Claims;
using System.Text.Json;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Infrastructure.Data;
using MEPBMmanager.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Controllers;

[ApiController]
[Route("api/games/{gameId}/orders")]
public class OrdersController : ControllerBase
{
    private readonly MepbmDbContext _db;

    public OrdersController(MepbmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(string gameId, [FromQuery] string? nationId = null)
    {
        var scope = await ResolveListScope(gameId, nationId);
        if (scope == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return Ok(new { orders = Array.Empty<object>(), turn = (object?)null });

        var orders = await _db.Orders
            .Include(o => o.Character)
            .Where(o => o.GameId == gameId && o.TurnId == currentTurn.Id && (scope == "*" || o.NationId == scope))
            .OrderBy(o => o.SubmittedAt)
            .ToListAsync();

        var orderDtos = orders.Select(o => new
        {
            o.Id,
            o.TurnId,
            o.NationId,
            o.CharacterId,
            o.ArmyId,
            o.Code,
            o.Parameters,
            o.Status,
            o.Result,
            o.SubmittedAt,
            character = new { name = o.Character.Name }
        });

        return Ok(new { orders = orderDtos, turn = new { currentTurn.Id, currentTurn.Number, currentTurn.Status, currentTurn.Season, currentTurn.Deadline } });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Submit(string gameId, [FromBody] SubmitOrderRequest request)
    {
        var nationId = await ResolveScopeNation(gameId, request.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = "No turn accepting orders" });

        var character = await _db.Characters
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.NationId == nationId);

        if (character == null)
            return NotFound(new { error = "Character not found" });

        if (character.IsDead || character.IsKidnapped)
            return BadRequest(new { error = "Character cannot act" });

        if (!IsValidOrderCode(request.Code))
            return BadRequest(new { error = "Invalid order code" });

        var pendingCount = await _db.Orders.CountAsync(o => o.GameId == gameId
            && o.TurnId == currentTurn.Id && o.CharacterId == request.CharacterId && o.Status == "pending");
        if (pendingCount >= 2)
            return BadRequest(new { error = "Character already has 2 orders this turn" });

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            GameId = gameId,
            TurnId = currentTurn.Id,
            NationId = nationId,
            CharacterId = request.CharacterId,
            ArmyId = request.ArmyId,
            NavyId = request.NavyId,
            Code = request.Code,
            Parameters = request.Parameters.HasValue ? request.Parameters.Value.GetRawText() : "{}",
            Status = "pending"
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            order = new
            {
                order.Id,
                order.GameId,
                order.TurnId,
                order.NationId,
                order.CharacterId,
                order.ArmyId,
                order.Code,
                order.Parameters,
                order.Status,
                order.Result,
                order.SubmittedAt
            }
        });
    }

    [HttpDelete("{orderId}")]
    [Authorize]
    public async Task<IActionResult> Cancel(string gameId, string orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var role = User.FindFirstValue(ClaimTypes.Role);
        var playerNation = await GetPlayerNationId(gameId);

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.GameId == gameId && o.Status == "pending");

        if (order == null)
            return NotFound(new { error = "Order not found or cannot be cancelled" });

        if (order.NationId != playerNation && !await IsStaff(gameId, userId, role))
            return StatusCode(403, new { error = "Not a player in this game" });

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Order cancelled" });
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> Validate(string gameId, [FromQuery] string? nationId = null)
    {
        var scope = await ResolveListScope(gameId, nationId);
        if (scope == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = "No turn accepting orders" });

        var orders = await _db.Orders
            .Include(o => o.Character)
            .Where(o => o.GameId == gameId && o.TurnId == currentTurn.Id && (scope == "*" || o.NationId == scope) && o.Status == "pending")
            .ToListAsync();

        var validationResults = orders.Select(order => new OrderValidationResult
        {
            OrderId = order.Id,
            CharacterName = order.Character.Name,
            OrderCode = order.Code,
            Valid = true,
            Errors = new List<string>()
        }).ToList();

        foreach (var order in orders)
        {
            var result = validationResults.First(r => r.OrderId == order.Id);
            if (order.Character.IsDead)
            {
                result.Valid = false;
                result.Errors.Add("Character is dead");
            }
            if (order.Character.IsKidnapped)
            {
                result.Valid = false;
                result.Errors.Add("Character is kidnapped");
            }
        }

        return Ok(new { results = validationResults });
    }

    // ── Elegibilidad: qué órdenes puede hacer el personaje ahora ──
    [HttpGet("eligible")]
    [Authorize]
    public async Task<IActionResult> Eligible(string gameId, [FromQuery] string characterId)
    {
        var nationId = await ResolveScopeNation(gameId, characterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.NationId == nationId);
        if (ch == null) return NotFound(new { error = "Character not found" });

        var nation = await _db.Nations
            .Include(n => n.Armies).Include(n => n.Navies)
            .FirstOrDefaultAsync(n => n.Id == nationId);
        var commandsNavy = nation?.Navies.Any(v => v.CommanderId == ch.Id) == true;

        var list = OrderDefinitions.Orders.Select(d =>
        {
            var (ok, reason) = CheckEligible(ch, d, nation, commandsNavy);
            return new { code = d.Code, ok, reason };
        }).ToList();
        return Ok(new { eligible = list });
    }

    private static (bool Ok, string Reason) CheckEligible(Character ch, OrderDefinition def,
        Nation? nation, bool commandsNavy)
    {
        if (ch.IsDead || ch.IsKidnapped) return (false, "Character cannot act");
        if (def.Code == 100) return (false, "Automatic order");
        var r = def.Restrictions;
        if (r.Length == 0 || r.Contains("without")) return (true, "");
        var type = (ch.Type ?? "").ToLower();
        foreach (var t in r)
        {
            switch (t)
            {
                case "c": if (type == "commander" || ch.CommandSkill > 0) return (true, ""); break;
                case "a": if (type == "agent" || ch.AgentSkill > 0) return (true, ""); break;
                case "e": if (type == "emissary" || ch.EmissarySkill > 0) return (true, ""); break;
                case "m": if (type == "mage" || ch.MageSkill > 0) return (true, ""); break;
                case "com":
                    if (ch.ArmyId != null || commandsNavy) return (true, "");
                    break;
                case "company": if (ch.CompanyId != null) return (true, ""); break;
                case "cap": if (ch.IsChampion) return (true, ""); break;
                case "fa": return (false, "Fourth Age only");
            }
        }
        // Prerrequisitos de estado por orden (los baratos de comprobar aquí)
        var need = def.Code switch
        {
            205 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id && TurnProcessor.CombatArtifactTypes.Contains(a.Type ?? "")) ? "" : "No combat artifact held",
            360 or 792 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id) ? "" : "No artifact held",
            750 or 760 => ch.CompanyId != null ? "" : "Not in a company",
            790 => ch.ArmyId != null ? "" : "Not in an army",
            120 => HasSpellType(ch, SpellType.Heal) ? "" : "No healing spell known",
            225 => HasSpellType(ch, SpellType.Combat) ? "" : "No combat spell known",
            330 => HasSpellType(ch, SpellType.Conjuring) ? "" : "No conjuring spell known",
            825 => HasSpellType(ch, SpellType.Movement) ? "" : "No movement spell known",
            940 => HasSpellType(ch, SpellType.Lore) ? "" : "No lore spell known",
            _ => ""
        };
        if (need != "") return (false, need);
        var armyNeeded = new HashSet<int> { 230, 235, 240, 250, 255, 260, 340, 345, 347, 349, 351, 353, 355, 370, 375, 400, 404, 408, 412, 416, 420, 425, 430, 435, 440, 444, 448, 452, 456, 765, 775, 850, 860 };
        if (armyNeeded.Contains(def.Code) && ch.ArmyId == null && (nation == null || nation.Armies.Count == 0))
            return (false, "No army available");
        var navyNeeded = new HashSet<int> { 270, 275, 280, 830, 794, 798 };
        if (navyNeeded.Contains(def.Code) && (nation == null || nation.Navies.Count == 0))
            return (false, "No navy available");
        return (false, "Requires " + string.Join("/", r));
    }

    private static bool HasSpellType(Character ch, SpellType type) =>
        ch.Spells.Any(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == type);

    // ── Estimación de orden (la UI compone formularios y muestra costes en vivo) ──
    public record OrderFieldOptionDto(string Value, string Label);
    public record OrderFieldSpecDto(string Key, string Label, string Kind, bool Required,
        int? Min = null, int? Max = null, string? Def = null, List<OrderFieldOptionDto>? Options = null);
    public record EstimateAfterOrderDto(int Code, System.Text.Json.JsonElement? Parameters);
    public record EstimateOrderRequest(string CharacterId, int Code, System.Text.Json.JsonElement? Parameters,
        string? ArmyId, string? NavyId, EstimateAfterOrderDto? AfterOrder);

    private static readonly HashSet<int> MoveCodes = new() { 810, 820, 830, 850, 860, 870, 825 };

    [HttpPost("estimate")]
    [Authorize]
    public async Task<IActionResult> Estimate(string gameId, [FromBody] EstimateOrderRequest req)
    {
        var nationId = await ResolveScopeNation(gameId, req.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == req.CharacterId && c.NationId == nationId);
        if (ch == null) return NotFound(new { error = "Character not found" });

        var game = await _db.Games
            .Include(g => g.Nations).ThenInclude(n => n.Characters)
            .Include(g => g.Nations).ThenInclude(n => n.Armies)
            .Include(g => g.Nations).ThenInclude(n => n.Navies)
            .Include(g => g.Nations).ThenInclude(n => n.PopulationCentres)
            .Include(g => g.Nations).ThenInclude(n => n.Relations)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return NotFound(new { error = "Game not found" });
        var nation = game.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation == null) return NotFound(new { error = "Nation not found" });

        var def = OrderDefinitions.Orders.FirstOrDefault(o => o.Code == req.Code);
        if (def == null) return BadRequest(new { error = "Invalid order code" });

        var pars = ParseEstimateParams(req.Parameters);
        string? afterDest = null;
        if (req.AfterOrder != null && MoveCodes.Contains(req.AfterOrder.Code))
        {
            var ap = ParseEstimateParams(req.AfterOrder.Parameters);
            if (ap.TryGetValue("destination", out var dEl) && dEl.ValueKind == System.Text.Json.JsonValueKind.String)
                afterDest = dEl.GetString();
        }
        var effLoc = afterDest ?? ch.LocationHex;

        var commandsNavy = nation.Navies.Any(v => v.CommanderId == ch.Id);
        var (okElig, reason) = CheckEligible(ch, def, nation, commandsNavy);
        if (!okElig)
            return Ok(new { ok = false, errors = new[] { reason }, costs = new { }, requires = Array.Empty<object>(),
                effectiveLocation = effLoc, movedByFirstOrder = afterDest != null });

        var army = ResolveEstimateArmy(req.ArmyId, ch, nation);
        var navy = ResolveEstimateNavy(req.NavyId, ch, nation);

        var ctx = new EstimateCtx(game, nation, ch, army, navy, effLoc, pars);
        var requires = RequiresFor(req.Code, ctx);
        var errors = ValidateEstimateParams(req.Code, ctx, requires);
        var pending = await PendingNationOrders(gameId, nation.Id);
        var used = SummarizePendingUsage(pending, nation, game);
        var costs = EstimateCosts(req.Code, ctx, out var maxAmount, out var expectedGold, used);
        if (maxAmount == 0)
            errors.Add("Insufficient resources or capacity to execute (max 0)");
        var warnings = new List<string>();
        CrossOrderConflicts(req.Code, ctx, pending, used, errors, warnings);

        object? suggestNames = null;
        if (req.Code is 552 or 555)
            suggestNames = TurnProcessor.CampSuggestions(nation.Name);

        return Ok(new
        {
            ok = errors.Count == 0,
            errors,
            warnings,
            costs,
            maxAmount,
            expectedGold,
            requires,
            effectiveLocation = effLoc,
            movedByFirstOrder = afterDest != null,
            suggestNames
        });
    }

    private sealed class EstimateCtx
    {
        public EstimateCtx(Game game, Nation nation, Character ch, Army? army, Navy? navy,
            string? effLoc, Dictionary<string, System.Text.Json.JsonElement> pars)
        { Game = game; Nation = nation; Ch = ch; Army = army; Navy = navy; EffLoc = effLoc; Pars = pars; }
        public Game Game { get; }
        public Nation Nation { get; }
        public Character Ch { get; }
        public Army? Army { get; }
        public Navy? Navy { get; }
        public string? EffLoc { get; }
        public Dictionary<string, System.Text.Json.JsonElement> Pars { get; }
    }

    private static Dictionary<string, System.Text.Json.JsonElement> ParseEstimateParams(System.Text.Json.JsonElement? el)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (el.HasValue && el.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            foreach (var p in el.Value.EnumerateObject()) d[p.Name] = p.Value;
        return d;
    }

    private Army? ResolveEstimateArmy(string? armyId, Character ch, Nation nation)
    {
        if (!string.IsNullOrEmpty(armyId))
            return nation.Armies.FirstOrDefault(a => a.Id == armyId);
        if (!string.IsNullOrEmpty(ch.ArmyId))
            return nation.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
        return nation.Armies.FirstOrDefault();
    }

    private Navy? ResolveEstimateNavy(string? navyId, Character ch, Nation nation)
    {
        if (!string.IsNullOrEmpty(navyId))
            return nation.Navies.FirstOrDefault(v => v.Id == navyId);
        return nation.Navies.FirstOrDefault(v => v.CommanderId == ch.Id)
            ?? nation.Navies.FirstOrDefault();
    }

    private static int ParamInt(Dictionary<string, System.Text.Json.JsonElement> pars, string key, int def = 0)
    {
        if (pars.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        return def;
    }

    private static string? ParamStr(Dictionary<string, System.Text.Json.JsonElement> pars, string key)
    {
        if (pars.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static OrderFieldSpecDto Num(string k, string l, bool req = true, int? min = null, int? max = null) =>
        new(k, l, "number", req, min, max);
    private static OrderFieldSpecDto Txt(string k, string l, bool req = true) =>
        new(k, l, "text", req);
    private static OrderFieldSpecDto Hx(string k, string l, bool req) =>
        new(k, l, "hex", req);
    private static OrderFieldSpecDto Sel(string k, string l, List<OrderFieldOptionDto> opts, bool req = true) =>
        new(k, l, "select", req, Options: opts);
    private static OrderFieldSpecDto Multi(string k, string l, List<OrderFieldOptionDto> opts) =>
        new(k, l, "multiselect", false, Options: opts);
    private static OrderFieldSpecDto Flag(string k, string l) =>
        new(k, l, "flag", false);

    private static readonly List<OrderFieldOptionDto> TacticOptions = new()
    {
        new("ch", "ch"), new("su", "su"), new("fl", "fl"),
        new("hr", "hr"), new("am", "am"), new("st", "st")
    };
    private static readonly List<OrderFieldOptionDto> AllegianceOptions = new()
    {
        new("free_peoples", "Free Peoples"), new("dark_servants", "Dark Servants"), new("neutral", "Neutral")
    };
    private static readonly List<OrderFieldOptionDto> MaterialOptions = new()
    {
        new("leather", "Leather (20)"), new("bronze", "Bronze (40)"),
        new("steel", "Steel (60)"), new("mithril", "Mithril (100)")
    };
    private static readonly List<OrderFieldOptionDto> TroopTypeOptions = new()
    {
        new("HeavyCavalry", "Heavy Cavalry"), new("LightCavalry", "Light Cavalry"),
        new("HeavyInfantry", "Heavy Infantry"), new("LightInfantry", "Light Infantry"),
        new("Archers", "Archers"), new("MenAtArms", "Men-at-Arms")
    };
    private static readonly List<OrderFieldOptionDto> ProductOptions =
        TurnProcessor.MarketProductList().Select(p => new OrderFieldOptionDto(p, p)).ToList();
    private static readonly List<OrderFieldOptionDto> LevelOptions = new()
    {
        new("1", "1 Tower"), new("2", "2 Fort"), new("3", "3 Castle"), new("4", "4 Keep"), new("5", "5 Citadel")
    };

    private List<OrderFieldSpecDto> RequiresFor(int code, EstimateCtx ctx)
    {
        var ch = ctx.Ch;
        List<OrderFieldOptionDto> SpellOpts(SpellType t) => ch.Spells
            .Where(s => s.IsKnown && !s.IsLost && SpellCatalog.Get(s.SpellId)?.Type == t)
            .Select(s => new OrderFieldOptionDto(s.SpellId.ToString(),
                $"#{s.SpellId} {SpellCatalog.Get(s.SpellId)?.Name ?? "?"}"))
            .ToList();
        List<OrderFieldOptionDto> ArmyOpts(string? atHex = null) => ctx.Nation.Armies
            .Where(a => atHex == null || a.LocationHex == atHex)
            .Select(a => new OrderFieldOptionDto(a.Id, $"{a.Name} @ {a.LocationHex}"))
            .ToList();
        List<OrderFieldOptionDto> CharOpts(IEnumerable<Character> chars) => chars
            .Select(c => new OrderFieldOptionDto(c.Id, $"{c.Name} ({c.Type} @ {c.LocationHex})"))
            .ToList();
        var ownChars = ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId == ctx.Nation.Id && !c.IsDead).ToList();
        var foeCharsAtLoc = ctx.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != ctx.Nation.Id && !c.IsDead && c.LocationHex == ctx.EffLoc).ToList();
        var allNations = ctx.Game.Nations
            .Select(n => new OrderFieldOptionDto(n.Id, $"{n.Name} ({n.Allegiance})")).ToList();
        var heldArts = ch.Artifacts.Where(a => a.HeldByCharacterId == ch.Id)
            .Select(a => new OrderFieldOptionDto(a.Id, a.Name)).ToList();
        var catalogSpells = SpellCatalog.All
            .Select(s => new OrderFieldOptionDto(s.Id.ToString(), $"#{s.Id} {s.Name}{(s.IsLost ? " [lost]" : "")}")).ToList();

        return code switch
        {
            175 => new() { Sel("allegiance", "Allegiance", AllegianceOptions) },
            180 or 185 or 500 => new() { Sel("nationId", code == 500 ? "Target nation" : "Nation", allNations) },
            690 => new() { Sel("nationId", "Victim nation", allNations), Num("amount", "Gold amount", min: 1) },
            210 or 615 or 620 => new() { Sel("targetId", "Target character", CharOpts(foeCharsAtLoc)) },
            225 or 120 or 330 or 940 => new() { Sel("spellId", "Spell",
                code == 225 ? SpellOpts(SpellType.Combat) : code == 120 ? SpellOpts(SpellType.Heal)
                : code == 330 ? SpellOpts(SpellType.Conjuring)
                : SpellOpts(SpellType.Lore)) },
            700 or 705 => new() { Sel("spellId", "Spell (empty = random research)", catalogSpells, req: false) },
            230 or 235 => new() { Sel("tactic", "Tactic", TacticOptions, req: false) },
            270 or 275 or 340 or 345 or 347 or 425 or 440 or 444 or 448 or 452 or 456 or 660
                => new() { Num("amount", "Amount", min: 1) },
            300 => new() { Num("newRate", "New tax rate", min: 10, max: 80) },
            349 or 351 or 353 or 355 => new() { Sel("destArmyId", "Destination army", ArmyOpts()), Num("amount", "Amount", min: 1) },
            370 or 375 => new() { Sel("material", "Material", MaterialOptions), Multi("troopTypes", "Troop types (empty = all present)", TroopTypeOptions), Num("amount", "Troops (empty = all)", req: false, min: 1) },
            400 or 404 or 408 or 412 or 416 or 420 => new() { Num("amount", "Troops", min: 1) },
            494 => new() { Sel("level", "Fort level (empty = next)", LevelOptions, req: false) },
            470 or 498 => new() { Num("amount", "Amount", req: false, min: 0) },
            725 or 728 or 731 or 734 or 737 or 745 or 770 => new() { Txt("name", "Name") },
            755 => new() { Sel("companyId", "Company", _db.Companies.Where(c => c.NationId == ctx.Nation.Id).ToList().Select(c => new OrderFieldOptionDto(c.Id, c.Name)).ToList()) },
            785 => new() { Sel("armyId", "Army", ArmyOpts()) },
            780 => new() { Sel("targetId", "New commander", CharOpts(ownChars)) },
            905 or 910 or 915 or 920 or 930 or 475 or 490 or 605 or 665 or 670 or 675 or 680 => new() { Hx("hex", "Hex (empty = current location)", req: false) },
            610 or 625 or 630 or 635 or 640 or 645 or 650 or 655 or 660 or 363 => new()
                { Sel("targetId", "Target", CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))) },
            685 => new()
                { Sel("artifactId", "Artifact", _db.Artifacts.Where(a => a.HeldByCharacterId != null && a.HeldByCharacterId != ch.Id).ToList().Select(a => new OrderFieldOptionDto(a.Id, a.Name)).ToList()) },
            505 => new() { Sel("targetId", "Target character", CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId != ctx.Nation.Id && !c.IsDead))), Num("amount", "Bribe gold (min 500)", req: false, min: 500) },
            552 or 555 => new() { Txt("name", "Camp name (empty = nation pool)", req: false) },
            520 or 525 or 530 or 535 or 550 or 560 or 565 or 580 or 585 or 949 => new() { Hx("hex", "Hex (empty = current location)", req: false) },
            360 => new() { Sel("artifactId", "Artifact", heldArts), Sel("targetId", "To character", CharOpts(ownChars)) },
            792 or 796 => new() { Sel("artifactId", "Artifact", heldArts) },
            810 or 820 or 830 or 850 or 860 or 870 => new() { Txt("destination", "Destination hex"), Flag("evasive", "Evasive") },
            825 => new() { Sel("spellId", "Spell", SpellOpts(SpellType.Movement)), Txt("destination", "Destination hex") },
            310 => new() { Sel("product", "Product", ProductOptions), Num("amount", "Amount", min: 1), Num("price", "Bid price (empty = market)", req: false, min: 1) },
            315 or 320 => new() { Sel("product", "Product", ProductOptions), Num("amount", "Amount", min: 1) },
            325 => new() { Sel("product", "Product", ProductOptions), Num("percentage", "Percentage of stock", min: 1, max: 100) },
            947 or 948 => new() { Sel("resource", "Resource", ProductOptions), Num("amount", "Amount", min: 1) },
            _ => new List<OrderFieldSpecDto>()
        };
    }

    private List<string> ValidateEstimateParams(int code, EstimateCtx ctx, List<OrderFieldSpecDto> requires)
    {
        var errors = new List<string>();
        var pars = ctx.Pars;
        foreach (var f in requires.Where(f => f.Required))
        {
            if (!pars.TryGetValue(f.Key, out var el) || el.ValueKind == System.Text.Json.JsonValueKind.Null
                || (el.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(el.GetString()))
                || (el.ValueKind == System.Text.Json.JsonValueKind.Array && !el.EnumerateArray().Any()))
                errors.Add($"Missing info: {f.Label}");
        }
        string? Str(string k) => ParamStr(pars, k);
        var ch = ctx.Ch;
        var gameChars = ctx.Game.Nations.SelectMany(n => n.Characters).ToList();
        Character? FindChar(string? id) => string.IsNullOrEmpty(id) ? null : gameChars.FirstOrDefault(c => c.Id == id);
        PopulationCentre? PcAt(string? hex) => hex == null ? null :
            ctx.Game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(p => p.LocationHex == hex);
        PopulationCentre? OwnPcAt(string? hex) => hex == null ? null :
            ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);

        if (code is 750 or 760 && ch.CompanyId == null) errors.Add("Not in a company");
        if (code == 790 && ch.ArmyId == null) errors.Add("Not in an army");
        if (code is 210 or 615 or 620)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add("Target character not found");
            else
            {
                if (code == 210 && t.LocationHex != ctx.EffLoc) errors.Add($"Target not in the same hex (at {t.LocationHex})");
                if (code == 620 && (t.IsDead || t.IsKidnapped)) errors.Add("Target cannot be kidnapped");
            }
        }
        if (code is 625 or 630 or 635 or 640 or 645 or 650 or 655)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add("Target not found");
            else if (!t.IsKidnapped) errors.Add("Target is not a hostage");
        }
        if (code is 685)
        {
            var a = Str("artifactId");
            if (a != null && !_db.Artifacts.Any(x => x.Id == a)) errors.Add("Artifact not found");
        }
        if (code is 690)
        {
            if (!ctx.Game.Nations.Any(n => n.Id == Str("nationId"))) errors.Add("Victim nation not found");
        }
        if (code is 180 or 185 && !ctx.Game.Nations.Any(n => n.Id == Str("nationId")))
            errors.Add("Nation not found");
        if (code == 175)
        {
            var al = (Str("allegiance") ?? "").ToLower();
            if (al != "free_peoples" && al != "dark_servants" && al != "neutral")
                errors.Add("Allegiance must be free_peoples, dark_servants or neutral");
        }
        if (code is 120 or 330 or 940 or 225 or 825)
        {
            var want = code == 120 ? SpellType.Heal : code == 330 ? SpellType.Conjuring
                : code == 940 ? SpellType.Lore : code == 825 ? SpellType.Movement : SpellType.Combat;
            var sid = ParamInt(pars, "spellId", -1);
            var def = SpellCatalog.Get(sid);
            if (def == null || def.Type != want || def.IsLost
                || !ch.Spells.Any(s => s.SpellId == sid && s.IsKnown && !s.IsLost))
                errors.Add($"Info must contain a valid known {want.ToString().ToLower()} spell");
        }
        if (code is 700 or 705 && pars.TryGetValue("spellId", out var sidEl)
            && sidEl.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            var sd = SpellCatalog.Get(sidEl.GetInt32());
            if (sd == null) errors.Add("Unknown spell");
            else if (code == 705 && sd.IsLost && !NationAbilities.CanLearnLostSpell(ctx.Nation.Name, sd.Id))
                errors.Add($"Lost spell {sd.Name} is not available to your nation");
        }
        if (code is 755 && Str("companyId") != null)
        {
            var cid = Str("companyId");
            if (!_db.Companies.Any(c => c.Id == cid && c.NationId == ctx.Nation.Id))
                errors.Add("Company not found");
        }
        if (code is 780 or 785)
        {
            var tid = code == 780 ? Str("targetId") : Str("armyId");
            if (code == 780 && FindChar(tid) == null) errors.Add("Target commander not found");
            if (code == 785 && !ctx.Nation.Armies.Any(a => a.Id == tid)) errors.Add("Army not found");
        }
        if (code is 520 && OwnPcAt(ctx.EffLoc) == null) errors.Add("Must be at one of your population centres");
        if (code == 525)
        {
            var pc = PcAt(ctx.EffLoc);
            if (pc == null) errors.Add("No population centre here");
            else if (pc.NationId == ctx.Nation.Id) errors.Add("Use 520 on your own centres");
            else if (pc.IsHidden) errors.Add("No visible population centre here");
        }
        if (code == 530)
        {
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null) errors.Add("Must be at one of your population centres");
            else if (!pc.HasHarbour || pc.HasPort) errors.Add("Needs a harbour (not yet a port) here");
        }
        if (code == 535 && OwnPcAt(ctx.EffLoc) == null) errors.Add("Must be at one of your population centres");
        if (code is 552 or 555 && OwnPcAt(ctx.EffLoc) != null) errors.Add("Already have a population centre here");
        if (code == 560 && !ctx.Nation.PopulationCentres.Any(p => p.LocationHex == ctx.EffLoc && p.Size == "camp"))
            errors.Add("No camp of yours here");
        if (code is 670 or 675 or 680 && PcAt(ParamStr(pars, "hex") ?? ctx.EffLoc) == null)
            errors.Add("No population centre here");
        if (code is 949 or 950)
        {
            var pc = ParamStr(pars, "pcId") != null
                ? ctx.Game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                : PcAt(ParamStr(pars, "hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add("No population centre found");
        }
        if (code is 310 or 315 or 320 or 325)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out _))
                errors.Add("Product must be timber, leather, bronze, steel, mithril, mounts or food");
        }
        if (code is 947 or 948)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "resource"), out _))
                errors.Add("Resource must be timber, leather, bronze, steel, mithril, mounts or food");
        }
        if (code is 475 or 490 or 665)
        {
            var tile = TileAt(gameId: ctx.Game.Id, hex: ParamStr(pars, "hex") ?? ctx.EffLoc ?? "");
            if (tile == null) errors.Add("No such hex");
            else if (code == 475 && !tile.HasBridge) errors.Add($"No bridge at {tile.Q},{tile.R}");
            else if (code == 665 && !tile.HasBridge) errors.Add($"No bridge at {tile.Q},{tile.R}");
            else if (code == 490 && !tile.HasMajorRiver && !tile.HasMinorRiver) errors.Add("No river here");
            else if (code == 490 && (tile.HasBridge || tile.HasFord)) errors.Add("A ford or bridge already exists here");
        }
        if (code == 494)
        {
            var pc = ParamStr(pars, "pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                : OwnPcAt(ParamStr(pars, "hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add("Must target one of your population centres");
        }
        if (code == 500)
        {
            var tid = Str("targetNationId");
            if (!ctx.Game.Nations.Any(n => n.Id == tid)) errors.Add("Target nation not found");
            else if (tid == ctx.Nation.Id) errors.Add("Cannot plant a double agent in your own nation");
        }
        if (code == 505 && FindChar(Str("targetId")) == null) errors.Add("Target not found");
        return errors;
    }

    private sealed class PendingUsage
    {
        public Dictionary<string, int> RecruitsByPc = new();
        public Dictionary<string, int> BuyByProduct = new();
        public int SellGoldUsed;
        public HashSet<string> FortifiedPcs = new();
        public HashSet<string> DoubleAgentNations = new();
        public HashSet<string> ChallengedTargets = new();
        public HashSet<string> BribedTargets = new();
        public HashSet<string> MovedArtifacts = new();
    }

    private static Dictionary<string, System.Text.Json.JsonElement> ParseStoredParams(string? json)
    {
        var d = new Dictionary<string, System.Text.Json.JsonElement>();
        if (string.IsNullOrWhiteSpace(json)) return d;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject()) d[p.Name] = p.Value.Clone();
        }
        catch { }
        return d;
    }

    private async Task<List<Order>> PendingNationOrders(string gameId, string nationId)
    {
        var turn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();
        if (turn == null) return new List<Order>();
        return await _db.Orders
            .Where(o => o.GameId == gameId && o.TurnId == turn.Id && o.NationId == nationId && o.Status == "pending")
            .ToListAsync();
    }

    private PendingUsage SummarizePendingUsage(List<Order> pending, Nation nation, Game game)
    {
        var used = new PendingUsage();
        var chars = game.Nations.SelectMany(n => n.Characters).ToDictionary(c => c.Id);
        foreach (var o in pending)
        {
            var p = ParseStoredParams(o.Parameters);
            int Amt(string k, int def = 0)
            {
                if (p.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var n2)) return n2;
                return def;
            }
            string? Str(string k) => p.TryGetValue(k, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : null;
            if (o.Code is >= 400 and <= 420)
            {
                var amount = Amt("amount");
                if (amount <= 0) continue;
                chars.TryGetValue(o.CharacterId, out var chx);
                var ax = o.ArmyId != null ? nation.Armies.FirstOrDefault(a => a.Id == o.ArmyId)
                    : chx?.ArmyId != null ? nation.Armies.FirstOrDefault(a => a.Id == chx.ArmyId)
                    : nation.Armies.FirstOrDefault();
                var pc = ax == null ? null : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == ax.LocationHex);
                if (pc != null) used.RecruitsByPc[pc.Id] = used.RecruitsByPc.GetValueOrDefault(pc.Id) + amount;
            }
            else if (o.Code is 310 or 315)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (prod != "") used.BuyByProduct[prod] = used.BuyByProduct.GetValueOrDefault(prod) + Amt("amount");
            }
            else if (o.Code is 320)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _) && Amt("amount") > 0)
                {
                    var (_, baseSell) = TurnProcessor.MarketRate(prod);
                    used.SellGoldUsed += Amt("amount") * NationAbilities.MarketSellPrice(nation.Name, baseSell);
                }
            }
            else if (o.Code is 325)
            {
                var prod = (Str("product") ?? "").ToLower();
                if (TurnProcessor.IsMarketProductName(prod, out _))
                {
                    var (_, baseSellAll) = TurnProcessor.MarketRate(prod);
                    var sell = NationAbilities.MarketSellPrice(nation.Name, baseSellAll);
                    var pct = Amt("percentage", 100);
                    var stock = StockOfLocal(nation, prod);
                    used.SellGoldUsed += stock * pct / 100 * sell;
                }
            }
            else if (o.Code == 494)
            {
                var pc = Str("pcId") != null
                    ? nation.PopulationCentres.FirstOrDefault(x => x.Id == Str("pcId"))
                    : nation.PopulationCentres.FirstOrDefault(x => x.LocationHex == (Str("hex") ?? chars.GetValueOrDefault(o.CharacterId)?.LocationHex));
                if (pc != null) used.FortifiedPcs.Add(pc.Id);
            }
            else if (o.Code == 500 && Str("targetNationId") is { } tn) used.DoubleAgentNations.Add(tn);
            else if (o.Code == 210 && Str("targetId") is { } tg) used.ChallengedTargets.Add(tg);
            else if (o.Code == 505 && Str("targetId") is { } tb) used.BribedTargets.Add(tb);
            else if ((o.Code == 360 || o.Code == 792) && Str("artifactId") is { } ar) used.MovedArtifacts.Add(ar);
        }
        return used;
    }

    private static int StockOfLocal(Nation n, string p) => p switch
    {
        "timber" => n.Timber, "leather" => n.Leather, "bronze" => n.Bronze,
        "steel" => n.Steel, "mithril" => n.Mithril, "mounts" => n.Mounts, "food" => n.Food, _ => 0
    };

    private void CrossOrderConflicts(int code, EstimateCtx ctx, List<Order> pending,
        PendingUsage used, List<string> errors, List<string> warnings)
    {
        var pars = ctx.Pars;
        string? Str(string k) => ParamStr(pars, k);
        if (code == 494)
        {
            var pc = Str("pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == Str("pcId"))
                : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == (Str("hex") ?? ctx.EffLoc));
            if (pc != null && used.FortifiedPcs.Contains(pc.Id))
                errors.Add($"{pc.Name} is already fortified by another order this turn");
        }
        if (code == 500 && Str("targetNationId") is { } tn && used.DoubleAgentNations.Contains(tn))
            errors.Add("Another order already recruits a double agent there");
        if (code == 210 && Str("targetId") is { } tg && used.ChallengedTargets.Contains(tg))
        {
            var otherMax = 0;
            foreach (var o in pending.Where(o => o.Code == 210))
            {
                var pp = ParseStoredParams(o.Parameters);
                if (pp.TryGetValue("targetId", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String && t.GetString() == tg)
                {
                    var oc = ctx.Game.Nations.SelectMany(n => n.Characters).FirstOrDefault(c => c.Id == o.CharacterId);
                    if (oc != null) otherMax = Math.Max(otherMax, oc.ChallengeRank);
                }
            }
            if (ctx.Ch.ChallengeRank < otherMax)
                errors.Add($"Target already challenged by higher challenge rank ({otherMax} vs yours {ctx.Ch.ChallengeRank})");
            else
                warnings.Add("Target already challenged by another character (only the highest CR fights)");
        }
        if (code == 505 && Str("targetId") is { } tb && used.BribedTargets.Contains(tb))
            warnings.Add("Target already bribed by another character (first attempt decides)");
        if ((code == 360 || code == 792) && ParamStr(pars, "artifactId") is { } ar && used.MovedArtifacts.Contains(ar))
            warnings.Add("Artifact already moved by another pending order");
        if ((code is 320 or 325) && used.SellGoldUsed > 0)
            warnings.Add($"{used.SellGoldUsed} gold of sell cap already used by other orders");
        if (code is 400 or 404 or 408 or 412 or 416 or 420)
        {
            var army = ParamStr(pars, "armyId") != null
                ? ctx.Nation.Armies.FirstOrDefault(a => a.Id == ParamStr(pars, "armyId"))
                : ctx.Nation.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId) ?? ctx.Nation.Armies.FirstOrDefault();
            var pc = army == null ? null : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == army.LocationHex);
            if (pc != null)
            {
                var left = TurnProcessor.RecruitCapacity(pc.Size) - used.RecruitsByPc.GetValueOrDefault(pc.Id);
                var want = ParamInt(pars, "amount", left);
                if (want > left)
                    errors.Add($"Only {Math.Max(0, left)} recruits left at {pc.Name} (other orders use {used.RecruitsByPc.GetValueOrDefault(pc.Id)})");
            }
        }
    }


    private object EstimateCosts(int code, EstimateCtx ctx, out int? maxAmount, out int? expectedGold, PendingUsage? used = null)
    {
        maxAmount = null;
        expectedGold = null;
        var costs = new Dictionary<string, int>();
        var n = ctx.Nation;
        var pars = ctx.Pars;
        Army? Army() => ParamStr(pars, "armyId") != null
            ? n.Armies.FirstOrDefault(a => a.Id == ParamStr(pars, "armyId"))
            : n.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId) ?? n.Armies.FirstOrDefault();
        PopulationCentre? OwnPc(string? hex) =>
            n.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);
        int TotalTroops(Army a) => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry
            + a.LightInfantry + a.Archers + a.MenAtArms;

        switch (code)
        {
            case 400 or 404 or 408 or 412 or 416 or 420:
            {
                var army = Army();
                if (army == null) break;
                var pc = OwnPc(army.LocationHex);
                if (pc == null) break;
                var unit = TurnProcessor.RecruitCostPerUnit[code];
                var cap = TurnProcessor.RecruitCapacity(pc.Size) - (used?.RecruitsByPc.GetValueOrDefault(pc.Id) ?? 0);
                var max = Math.Min(cap, n.Gold / unit);
                if (code is 400 or 404) max = Math.Min(max, n.Mounts);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * unit;
                if (code is 400 or 404) costs["mounts"] = amount;
                break;
            }
            case 452 or 456:
            {
                var army = Army();
                var pc = army == null ? null : OwnPc(army.LocationHex);
                if (pc == null || (!pc.HasPort && !pc.HasHarbour)) break;
                var timberEach = NationAbilities.ShipTimberCost(n.Name);
                var max = Math.Min(n.Timber / timberEach, n.Gold / 1000);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 1000;
                costs["timber"] = amount * timberEach;
                break;
            }
            case 440:
            {
                if (Army() == null || OwnPc(Army()!.LocationHex) == null) break;
                var max = Math.Min(n.Gold / 50, Math.Min(n.Timber / 10, n.Steel / 5));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 50;
                costs["timber"] = amount * 10;
                costs["steel"] = amount * 5;
                break;
            }
            case 444:
            {
                var army = Army();
                if (army == null || OwnPc(army.LocationHex) == null) break;
                var headroom = Math.Max(0, 100 - army.HCArmourRank);
                var max = Math.Min(headroom, Math.Min(n.Gold / 5, Math.Min(n.Leather / 5, n.Steel / 2)));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 5;
                costs["leather"] = amount * 5;
                costs["steel"] = amount * 2;
                break;
            }
            case 448:
            {
                var army = Army();
                if (army == null || OwnPc(army.LocationHex) == null) break;
                var headroom = Math.Max(0, 100 - army.HCWeaponRank);
                var max = Math.Min(headroom, Math.Min(n.Gold / 5, Math.Min(n.Bronze / 3, n.Steel / 1)));
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * 5;
                costs["bronze"] = amount * 3;
                costs["steel"] = amount;
                break;
            }
            case 370 or 375:
            {
                if (Army() == null) break;
                var material = (ParamStr(pars, "material") ?? "bronze").ToLower();
                var present = TotalTroops(Army()!);
                maxAmount = present;
                var amount = Math.Min(ParamInt(pars, "amount", present), present);
                costs["gold"] = code == 370 ? 500 : 600;
                costs[material] = Math.Max(1, (amount + 99) / 100);
                break;
            }
            case 494:
            {
                var pc = ParamStr(pars, "pcId") != null
                    ? n.PopulationCentres.FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                    : OwnPc(ParamStr(pars, "hex") ?? ctx.EffLoc);
                if (pc == null) break;
                var ladder = new[] { "tower", "fort", "castle", "keep", "citadel" };
                var cur = FortLadderIndexLocal(pc.Fortification);
                if (cur + 1 > 4) break;
                var fort = ladder[cur + 1];
                costs["timber"] = NationAbilities.FortTimberCost(n.Name, fort);
                costs["gold"] = NationAbilities.FortGoldCost(fort);
                break;
            }
            case 530: costs["gold"] = 1500; costs["timber"] = 2500; break;
            case 535: costs["gold"] = 2500; costs["timber"] = 5000; break;
            case 555: costs["gold"] = 2000; break;
            case 552: costs["gold"] = 4000; break;
            case 745: costs["gold"] = 500; break;
            case 770: costs["gold"] = NationAbilities.HireArmyCost(n.Name); break;
            case 950: costs["gold"] = 25000; break;
            case 505: costs["gold"] = Math.Max(500, ParamInt(pars, "amount", 500)); break;
            case 270 or 275:
            {
                var navy = n.Navies.FirstOrDefault();
                if (navy != null) maxAmount = code == 270 ? navy.Warships : navy.Transports;
                break;
            }
            case 425:
            {
                var army = Army();
                if (army == null) break;
                maxAmount = TotalTroops(army);
                var amount = Math.Min(ParamInt(pars, "amount", maxAmount.Value), maxAmount.Value);
                expectedGold = amount * 10;
                break;
            }
            case 470:
            {
                var pc = OwnPc(ctx.EffLoc);
                if (pc == null) break;
                maxAmount = pc.Stores;
                break;
            }
            case 310 or 315:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (baseBuy, _) = TurnProcessor.MarketRate(product);
                var buy = code == 310
                    ? Math.Max(NationAbilities.MarketBuyPrice(n.Name, baseBuy), ParamInt(pars, "price", NationAbilities.MarketBuyPrice(n.Name, baseBuy)))
                    : NationAbilities.MarketBuyPrice(n.Name, baseBuy);
                var poolLeft = Math.Max(0, TurnProcessor.MarketBuyPoolFor(product) - (used?.BuyByProduct.GetValueOrDefault(product) ?? 0));
                var max = Math.Min(n.Gold / Math.Max(1, buy), poolLeft);
                maxAmount = max;
                var amount = Math.Min(ParamInt(pars, "amount", max), max);
                costs["gold"] = amount * buy;
                break;
            }
            case 320:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (_, baseSell) = TurnProcessor.MarketRate(product);
                var sell = NationAbilities.MarketSellPrice(n.Name, baseSell);
                var stock = StockOf(n, product);
                maxAmount = stock;
                var amount = Math.Min(ParamInt(pars, "amount", stock), stock);
                costs[product] = amount;
                var remaining = TurnProcessor.MarketSellCapGold() - (used?.SellGoldUsed ?? 0);
                expectedGold = Math.Max(0, Math.Min(amount * sell, remaining));
                break;
            }
            case 325:
            {
                if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out var product)) break;
                var (_, baseSellAll) = TurnProcessor.MarketRate(product);
                var sell = NationAbilities.MarketSellPrice(n.Name, baseSellAll);
                var stock = StockOf(n, product);
                maxAmount = stock;
                var pct = pars.TryGetValue("percentage", out var pe) && pe.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? Math.Clamp(pe.GetInt32(), 0, 100) : 100;
                var amount = stock * pct / 100;
                costs[product] = amount;
                var remainingAll = TurnProcessor.MarketSellCapGold() - (used?.SellGoldUsed ?? 0);
                expectedGold = Math.Max(0, Math.Min(amount * sell, remainingAll));
                break;
            }
        }
        return costs;
    }

    private static int FortLadderIndexLocal(string? fort) => (fort ?? "").ToLower() switch
    {
        "tower" or "palisade" => 0,
        "fort" => 1,
        "stone walls" or "castle" => 2,
        "walls" or "keep" or "fortress" => 3,
        "citadel walls" or "citadel" => 4,
        _ => -1
    };

    private static int StockOf(Nation n, string p) => p switch
    {
        "timber" => n.Timber, "leather" => n.Leather, "bronze" => n.Bronze,
        "steel" => n.Steel, "mithril" => n.Mithril, "mounts" => n.Mounts, "food" => n.Food, _ => 0
    };

    private HexTile? TileAt(string gameId, string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r)) return null;
        return _db.HexTiles.FirstOrDefault(h => h.GameId == gameId && h.Q == q && h.R == r);
    }

    private async Task<string?> GetPlayerNationId(string gameId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId);
        return player?.NationId;
    }

    private async Task<bool> IsStaff(string gameId, string userId, string? role) =>
        role == "test_admin" || await _db.GameAdmins.AnyAsync(ga => ga.GameId == gameId && ga.UserId == userId);

    // Nación sobre la que se opera: la propia si eres jugador; si eres staff
    // (test_admin o game-admin), la del personaje indicado (para operar PNJs).
    private async Task<string?> ResolveScopeNation(string gameId, string? characterId = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var role = User.FindFirstValue(ClaimTypes.Role);
        var playerNation = await GetPlayerNationId(gameId);
        if (playerNation != null) return playerNation;
        if (!await IsStaff(gameId, userId, role) || characterId == null) return null;
        var charNation = await _db.Characters
            .Where(c => c.Id == characterId).Select(c => c.NationId).FirstOrDefaultAsync();
        if (charNation == null) return null;
        return await _db.Nations.AnyAsync(n => n.Id == charNation && n.GameId == gameId) ? charNation : null;
    }

    // Scope para listados: nación propia, "*" (todo el juego) para staff,
    // o una nación concreta (?nationId=) validada para staff.
    private async Task<string?> ResolveListScope(string gameId, string? nationId)
    {
        var playerNation = await GetPlayerNationId(gameId);
        if (playerNation != null) return playerNation;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!await IsStaff(gameId, userId, role)) return null;
        if (nationId == null) return "*";
        return await _db.Nations.AnyAsync(n => n.Id == nationId && n.GameId == gameId) ? nationId : null;
    }

    private static bool IsValidOrderCode(int code) =>
        OrderDefinitions.Orders.Any(o => o.Code == code);
}

public record SubmitOrderRequest(string CharacterId, int Code, System.Text.Json.JsonElement? Parameters, string? ArmyId, string? NavyId);
public record OrderValidationResult
{
    public string OrderId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public int OrderCode { get; set; }
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
}
