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
            .OrderBy(o => o.Code).ThenBy(o => o.SubmittedAt)
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
    public async Task<IActionResult> Submit(string gameId, [FromBody] SubmitOrderRequest request, [FromQuery] string? lang = null)
    {
        var lng = NormLang(lang);
        var nationId = await ResolveScopeNation(gameId, request.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = L(lng, "No turn accepting orders", "Ningún turno acepta órdenes") });

        var character = await _db.Characters
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.NationId == nationId);

        if (character == null)
            return NotFound(new { error = L(lng, "Character not found", "Personaje no encontrado") });

        if (character.IsDead || character.IsKidnapped)
            return BadRequest(new { error = L(lng, "Character cannot act", "El personaje no puede actuar") });

        if (!IsValidOrderCode(request.Code))
            return BadRequest(new { error = L(lng, "Invalid order code", "Código de orden inválido") });

        var pendingCount = await _db.Orders.CountAsync(o => o.GameId == gameId
            && o.TurnId == currentTurn.Id && o.CharacterId == request.CharacterId && o.Status == "pending");
        if (pendingCount >= 2)
            return BadRequest(new { error = L(lng, "Character already has 2 orders this turn", "El personaje ya tiene 2 órdenes este turno") });

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            GameId = gameId,
            TurnId = currentTurn.Id,
            NationId = nationId,
            CharacterId = request.CharacterId,
            ArmyId = request.ArmyId ?? character.ArmyId,
            NavyId = request.NavyId ?? (request.Code == 830 ? await _db.Navies
                .Where(v => v.NationId == nationId && v.CommanderId == character.Id)
                .Select(v => v.Id).FirstOrDefaultAsync() : null),
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
    public async Task<IActionResult> Validate(string gameId, [FromQuery] string? nationId = null, [FromQuery] string? lang = null)
    {
        var lng = NormLang(lang);
        var scope = await ResolveListScope(gameId, nationId);
        if (scope == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = L(lng, "No turn accepting orders", "Ningún turno acepta órdenes") });

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
                result.Errors.Add(L(lng, "Character is dead", "El personaje está muerto"));
            }
            if (order.Character.IsKidnapped)
            {
                result.Valid = false;
                result.Errors.Add(L(lng, "Character is kidnapped", "El personaje está secuestrado"));
            }
        }

        return Ok(new { results = validationResults });
    }

    // ── Elegibilidad: qué órdenes puede hacer el personaje ahora ──
    [HttpGet("eligible")]
    [Authorize]
    public async Task<IActionResult> Eligible(string gameId, [FromQuery] string characterId, [FromQuery] string? lang = null)
    {
        var nationId = await ResolveScopeNation(gameId, characterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.NationId == nationId);
        if (ch == null) return NotFound(new { error = "Character not found" });

        var nation = await _db.Nations
            .Include(n => n.Armies).Include(n => n.Navies).Include(n => n.PopulationCentres)
            .FirstOrDefaultAsync(n => n.Id == nationId);
        var commandsNavy = nation?.Navies.Any(v => v.CommanderId == ch.Id) == true;

        var lng = NormLang(lang);
        var list = OrderDefinitions.Orders.Select(d =>
        {
            var (ok, reason) = CheckEligible(ch, d, nation, commandsNavy, lng);
            return new { code = d.Code, ok, reason };
        }).ToList();
        return Ok(new { eligible = list });
    }

    private static (bool Ok, string Reason) CheckEligible(Character ch, OrderDefinition def,
        Nation? nation, bool commandsNavy, string? lang = null)
    {
        if (ch.IsDead || ch.IsKidnapped) return (false, L(lang, "Character cannot act", "El personaje no puede actuar"));
        if (def.Code == 100) return (false, L(lang, "Automatic order", "Orden automática"));
        // Prerrequisitos de estado: van antes que las restricciones de tipo,
        // aplican a todas las órdenes.
        var need0 = def.Code switch
        {
            205 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id && TurnProcessor.CombatArtifactTypes.Contains(a.Type ?? "")) ? "" : L(lang, "No combat artifact held", "Sin artefacto de combate en mano"),
            360 or 792 => ch.Artifacts.Any(a => a.HeldByCharacterId == ch.Id) ? "" : L(lang, "No artifact held", "Sin artefacto en mano"),
            750 or 760 => ch.CompanyId != null ? "" : L(lang, "Not in a company", "Fuera de compañía"),
            790 => ch.ArmyId != null ? "" : L(lang, "Not in an army", "Fuera del ejército"),
            120 => HasSpellType(ch, SpellType.Heal) ? "" : L(lang, "No healing spell known", "Sin hechizo de curación"),
            225 => HasSpellType(ch, SpellType.Combat) ? "" : L(lang, "No combat spell known", "Sin hechizo de combate"),
            330 => HasSpellType(ch, SpellType.Conjuring) ? "" : L(lang, "No conjuring spell known", "Sin hechizo de invocación"),
            825 => HasSpellType(ch, SpellType.Movement) ? "" : L(lang, "No movement spell known", "Sin hechizo de movimiento"),
            940 => HasSpellType(ch, SpellType.Lore) ? "" : L(lang, "No lore spell known", "Sin hechizo de saber"),
            _ => ""
        };
        if (need0 != "") return (false, need0);
        var armyNeeded = new HashSet<int> { 230, 235, 240, 250, 255, 260, 340, 345, 347, 349, 351, 353, 355, 370, 375, 400, 404, 408, 412, 416, 420, 425, 430, 435, 440, 444, 448, 765, 775, 780, 840, 850, 860 };
        if (armyNeeded.Contains(def.Code) && ch.ArmyId == null)
            return (false, L(lang, "Not in an army", "Fuera del ejército"));
        var navyNeeded = new HashSet<int> { 270, 275, 280, 830, 794, 798 };
        if (navyNeeded.Contains(def.Code) && (nation == null || nation.Navies.Count == 0))
            return (false, L(lang, "No navy available", "Sin armada disponible"));
        if (def.Code == 830 && !commandsNavy)
            return (false, L(lang, "Must command a navy", "Debes mandar una armada"));
        var capHex = nation?.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
        var atCapital = capHex != null && capHex == ch.LocationHex;
        if ((def.Code is 175 or 180 or 185 or 280 or 300 or 325 or 660 or 725 or 728 or 731 or 734 or 737) && !atCapital)
            return (false, L(lang, "Must be at your own capital", "Debes estar en tu capital"));
        if (def.Code == 950 && ch.LocationHex != capHex)
            return (false, L(lang, "Must be at your current capital", "Debes estar en tu capital actual"));
        if ((def.Code is 520 or 530 or 535 or 550 or 705 or 710)
            && (nation == null || !nation.PopulationCentres.Any(p => p.LocationHex == ch.LocationHex && p.NationId == nation.Id)))
            return (false, L(lang, "Must be at one of your population centres", "Debes estar en un centro propio"));
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
                case "fa": return (false, L(lang, "Fourth Age only", "Solo Cuarta Edad"));
            }
        }
        return (false, L(lang, "Requires ", "Requiere ") + string.Join("/", r));
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
    public async Task<IActionResult> Estimate(string gameId, [FromBody] EstimateOrderRequest req, [FromQuery] string? lang = null)
    {
        var lng = NormLang(lang);
        var nationId = await ResolveScopeNation(gameId, req.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var ch = await _db.Characters
            .Include(c => c.Spells).Include(c => c.Artifacts)
            .FirstOrDefaultAsync(c => c.Id == req.CharacterId && c.NationId == nationId);
        if (ch == null) return NotFound(new { error = L(lng, "Character not found", "Personaje no encontrado") });

        var game = await _db.Games
            .AsSplitQuery()
            .Include(g => g.Nations).ThenInclude(n => n.Characters)
            .Include(g => g.Nations).ThenInclude(n => n.Armies)
            .Include(g => g.Nations).ThenInclude(n => n.Navies)
            .Include(g => g.Nations).ThenInclude(n => n.PopulationCentres)
            .Include(g => g.Nations).ThenInclude(n => n.Relations)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return NotFound(new { error = L(lng, "Game not found", "Partida no encontrada") });
        var nation = game.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation == null) return NotFound(new { error = L(lng, "Nation not found", "Nación no encontrada") });

        var def = OrderDefinitions.Orders.FirstOrDefault(o => o.Code == req.Code);
        if (def == null) return BadRequest(new { error = L(lng, "Invalid order code", "Código de orden inválido") });

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
        var (okElig, reason) = CheckEligible(ch, def, nation, commandsNavy, lng);
        if (!okElig)
            return Ok(new { ok = false, errors = new[] { reason }, costs = new { }, requires = Array.Empty<object>(),
                effectiveLocation = effLoc, movedByFirstOrder = afterDest != null });

        var army = ResolveEstimateArmy(req.ArmyId, ch, nation);
        var navy = ResolveEstimateNavy(req.NavyId, ch, nation);

        var ctx = new EstimateCtx(game, nation, ch, army, navy, effLoc, pars, lng);
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

    private static string L(string? lang, string en, string es) => lang == "es" ? es : en;
    private static string NormLang(string? lang) => lang == "es" ? "es" : "en";
    private static string SpellTypeEs(SpellType t) => t switch
    {
        SpellType.Heal => "curación",
        SpellType.Combat => "combate",
        SpellType.Conjuring => "invocación",
        SpellType.Movement => "movimiento",
        SpellType.Lore => "saber",
        _ => "hechizo"
    };
    private static string CostKey(string? lang, string key) => key.ToLower() switch
    {
        "gold" => L(lang, "gold", "oro"),
        "food" => L(lang, "food", "comida"),
        "timber" => L(lang, "timber", "madera"),
        "leather" => L(lang, "leather", "cuero"),
        "bronze" => L(lang, "bronze", "bronce"),
        "steel" => L(lang, "steel", "acero"),
        "mithril" => L(lang, "mithril", "mitril"),
        "mounts" => L(lang, "mounts", "monturas"),
        _ => key
    };

    private sealed class EstimateCtx
    {
        public EstimateCtx(Game game, Nation nation, Character ch, Army? army, Navy? navy,
            string? effLoc, Dictionary<string, System.Text.Json.JsonElement> pars, string lang)
        { Game = game; Nation = nation; Ch = ch; Army = army; Navy = navy; EffLoc = effLoc; Pars = pars; Lang = lang; }
        public Game Game { get; }
        public Nation Nation { get; }
        public Character Ch { get; }
        public Army? Army { get; }
        public Navy? Navy { get; }
        public string? EffLoc { get; }
        public Dictionary<string, System.Text.Json.JsonElement> Pars { get; }
        public string Lang { get; }
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
        return null;
    }

    private Navy? ResolveEstimateNavy(string? navyId, Character ch, Nation nation)
    {
        if (!string.IsNullOrEmpty(navyId))
            return nation.Navies.FirstOrDefault(v => v.Id == navyId);
        return nation.Navies.FirstOrDefault(v => v.CommanderId == ch.Id);
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
    private static List<OrderFieldOptionDto> AllegianceOptions(string? lang) => new()
    {
        new("free_peoples", L(lang, "Free Peoples", "Pueblos Libres")),
        new("dark_servants", L(lang, "Dark Servants", "Sirvientes Oscuros")),
        new("neutral", L(lang, "Neutral", "Neutral"))
    };
    private static List<OrderFieldOptionDto> MaterialOptions(string? lang) => new()
    {
        new("leather", L(lang, "Leather (10)", "Cuero (10)")), new("bronze", L(lang, "Bronze (30)", "Bronce (30)")),
        new("steel", L(lang, "Steel (60)", "Acero (60)")), new("mithril", L(lang, "Mithril (100)", "Mitril (100)"))
    };
    private static List<OrderFieldOptionDto> TroopTypeOptions(string? lang) => new()
    {
        new("HeavyCavalry", L(lang, "Heavy Cavalry", "Caballería pesada")),
        new("LightCavalry", L(lang, "Light Cavalry", "Caballería ligera")),
        new("HeavyInfantry", L(lang, "Heavy Infantry", "Infantería pesada")),
        new("LightInfantry", L(lang, "Light Infantry", "Infantería ligera")),
        new("Archers", L(lang, "Archers", "Arqueros")),
        new("MenAtArms", L(lang, "Men-at-Arms", "Hombres de armas"))
    };
    private static List<OrderFieldOptionDto> ProductOptions(string? lang) =>
        TurnProcessor.MarketProductList().Select(p => new OrderFieldOptionDto(p, ProductName(lang, p))).ToList();
    private static string ProductName(string? lang, string p) => p.ToLower() switch
    {
        "timber" => L(lang, "timber", "madera"),
        "leather" => L(lang, "leather", "cuero"),
        "bronze" => L(lang, "bronze", "bronce"),
        "steel" => L(lang, "steel", "acero"),
        "mithril" => L(lang, "mithril", "mitril"),
        "mounts" => L(lang, "mounts", "monturas"),
        "food" => L(lang, "food", "comida"),
        _ => p
    };
    private static List<OrderFieldOptionDto> LevelOptions(string? lang) => new()
    {
        new("1", L(lang, "1 Tower", "1 Torre")), new("2", L(lang, "2 Fort", "2 Fuerte")),
        new("3", L(lang, "3 Castle", "3 Castillo")), new("4", L(lang, "4 Keep", "4 Torreón")),
        new("5", L(lang, "5 Citadel", "5 Ciudadela"))
    };
    private static string AllegianceName(string? lang, string? al) => (al ?? "").ToLower() switch
    {
        "free_peoples" or "free" => L(lang, "Free Peoples", "Pueblos Libres"),
        "dark_servants" or "dark" => L(lang, "Dark Servants", "Sirvientes Oscuros"),
        "neutral" => L(lang, "Neutral", "Neutral"),
        _ => al ?? "?"
    };
    private static readonly Dictionary<string, string> NationEs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Woodmen"] = "Hombres del Bosque",
        ["Northmen"] = "Hombres del Norte",
        ["Riders of Rohan"] = "Jinetes de Rohan",
        ["Dúnadan Rangers"] = "Montaraces Dúnedain",
        ["Dunadan Rangers"] = "Montaraces Dúnedain",
        ["Silvan Elves"] = "Elfos Silvanos",
        ["Northern Gondor"] = "Gondor del Norte",
        ["Southern Gondor"] = "Gondor del Sur",
        ["Dwarves"] = "Enanos",
        ["Sinda Elves"] = "Elfos Sindar",
        ["Noldo Elves"] = "Elfos Noldor",
        ["Witch-king"] = "Rey Brujo",
        ["Witch King"] = "Rey Brujo",
        ["Dragon Lord"] = "Señor de los Dragones",
        ["Dog Lord"] = "Señor de los Perros",
        ["Cloud Lord"] = "Señor de las Nubes",
        ["Blind Sorcerer"] = "Hechicero Ciego",
        ["Ice King"] = "Rey de Hielo",
        ["Quiet Avenger"] = "Vengador Silencioso",
        ["Fire King"] = "Rey del Fuego",
        ["Long Rider"] = "Jinete Largo",
        ["Dark Lieutenants"] = "Lugartenientes Oscuros",
        ["Corsairs"] = "Corsarios",
        ["Rhûn Easterlings"] = "Orientales de Rhûn",
        ["Rhun Easterlings"] = "Orientales de Rhûn",
        ["Dunlendings"] = "Dunlendinos",
        ["White Wizard"] = "Mago Blanco",
        ["Khand Easterlings"] = "Orientales de Khand",
    };
    private static string NationDisplayName(string? lang, string name) =>
        lang == "es" && NationEs.TryGetValue(name ?? "", out var es) ? es : name;
    private static string CharTypeName(string? lang, string? t) => (t ?? "").ToLower() switch
    {
        "commander" => L(lang, "commander", "comandante"),
        "agent" => L(lang, "agent", "agente"),
        "emissary" => L(lang, "emissary", "emisario"),
        "mage" => L(lang, "mage", "mago"),
        _ => t ?? "?"
    };

    // Hechizos de lore por tipo de diana (wiki: Required Information).
    private static readonly HashSet<int> LoreCharSpells = new() { 408, 420, 422, 424, 430, 436 };
    private static readonly HashSet<int> LoreCommanderSpells = new() { 406, 417, 426 };
    private static readonly HashSet<int> LoreNationSpells = new() { 404, 419, 432 };
    private static readonly HashSet<int> LoreAllegianceSpells = new() { 402, 410 };
    private static readonly HashSet<int> LoreArtifactSpells = new() { 412, 418, 428 };

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
        var lng = ctx.Lang;
        List<OrderFieldOptionDto> CharOpts(IEnumerable<Character> chars) => chars
            .Select(c => new OrderFieldOptionDto(c.Id, $"{c.Name} ({CharTypeName(lng, c.Type)} @ {c.LocationHex})"))
            .ToList();
        var ownChars = ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId == ctx.Nation.Id && !c.IsDead).ToList();
        var foeCharsAtLoc = ctx.Game.Nations.SelectMany(n => n.Characters)
            .Where(c => c.NationId != ctx.Nation.Id && !c.IsDead && c.LocationHex == ctx.EffLoc).ToList();
        var allNations = ctx.Game.Nations
            .Select(n => new OrderFieldOptionDto(n.Id, $"{NationDisplayName(lng, n.Name)} ({AllegianceName(lng, n.Allegiance)})")).ToList();
        var heldArts = ch.Artifacts.Where(a => a.HeldByCharacterId == ch.Id)
            .Select(a => new OrderFieldOptionDto(a.Id, a.Name)).ToList();
        var catalogSpells = SpellCatalog.All
            .Select(s => new OrderFieldOptionDto(s.Id.ToString(), $"#{s.Id} {s.Name}{(s.IsLost ? L(lng, " [lost]", " [perdido]") : "")}")).ToList();
        // 940: el formulario se criba según el hechizo elegido.
        List<OrderFieldSpecDto> LoreRequires()
        {
            var fields = new List<OrderFieldSpecDto>
                { Sel("spellId", L(lng, "Spell", "Hechizo"), SpellOpts(SpellType.Lore)) };
            var sid = ParamInt(ctx.Pars, "spellId", -1);
            if (LoreCharSpells.Contains(sid))
                fields.Add(Sel("targetId", L(lng, "Target character", "Personaje objetivo"),
                    CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))));
            else if (LoreCommanderSpells.Contains(sid))
                fields.Add(Sel("commanderId", L(lng, "Force commander", "Comandante de fuerza"),
                    CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead
                        && (c.ArmyId != null || ctx.Game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id))))));
            else if (LoreNationSpells.Contains(sid))
                fields.Add(Sel("nationId", L(lng, "Target nation", "Nación objetivo"), allNations));
            else if (LoreAllegianceSpells.Contains(sid))
                fields.Add(Sel("allegiance", L(lng, "Allegiance", "Lealtad"), AllegianceOptions(lng)));
            else if (LoreArtifactSpells.Contains(sid))
                fields.Add(Sel("artifactId", L(lng, "Artifact", "Artefacto"), _db.Artifacts.ToList()
                    .Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId != null
                        ? $"{a.Name} {L(lng, "(held)", "(en mano)")}" : $"{a.Name} @ {a.LocationHex ?? "?"}")).ToList()));
            else
                fields.Add(Hx("hex", L(lng, "Hex (empty = here)", "Hex (vacío = aquí)"), req: false));
            return fields;
        }

        return code switch
        {
            175 => new() { Sel("allegiance", L(lng, "Allegiance", "Lealtad"), AllegianceOptions(lng)) },
            180 or 185 => new() { Sel("nationId", L(lng, "Nation", "Nación"), allNations) },
            690 => new() { Sel("nationId", L(lng, "Victim nation", "Nación víctima"), allNations), Num("amount", L(lng, "Gold amount", "Oro"), min: 1) },
            210 or 615 or 620 => new() { Sel("targetId", L(lng, "Target character", "Personaje objetivo"), CharOpts(foeCharsAtLoc)) },
            225 or 330 => new() { Sel("spellId", L(lng, "Spell", "Hechizo"),
                code == 225 ? SpellOpts(SpellType.Combat) : SpellOpts(SpellType.Conjuring)) },
            120 => new() { Sel("spellId", L(lng, "Spell", "Hechizo"), SpellOpts(SpellType.Heal)),
                Sel("targetId", L(lng, "Target character (same hex, empty = self)", "Objetivo (mismo hex, vacío = uno mismo)"), CharOpts(ctx.Game.Nations
                    .SelectMany(n => n.Characters).Where(c => !c.IsDead && c.LocationHex == ctx.EffLoc)), req: false) },
            705 => new() { Sel("spellId", L(lng, "Spell (empty = random research)", "Hechizo (vacío = aleatorio)"), SpellCatalog.All
                .Where(s => !ch.Spells.Any(x => x.SpellId == s.Id && x.IsKnown && !x.IsLost)
                    && (!s.IsLost || NationAbilities.CanLearnLostSpell(ctx.Nation.Name, s.Id)))
                .Select(s => new OrderFieldOptionDto(s.Id.ToString(), $"#{s.Id} {s.Name}{(s.IsLost ? L(lng, " [lost]", " [perdido]") : "")}")).ToList(), req: false) },
            230 or 235 => new() { Sel("tactic", L(lng, "Tactic", "Táctica"), TacticOptions, req: false) },
            270 or 340 or 345 or 347 or 440 or 452 or 456
                => new() { Num("amount", L(lng, "Amount", "Cantidad"), min: 1) },
            275 => new() { Num("warships", L(lng, "Warships (empty = all)", "Buques (vacío = todos)"), req: false, min: 0), Num("transports", L(lng, "Transports (empty = all)", "Transportes (vacío = todos)"), req: false, min: 0) },
            280 => new() { Num("warships", L(lng, "Warships (empty = all)", "Buques (vacío = todos)"), req: false, min: 0), Num("transports", L(lng, "Transports (empty = all)", "Transportes (vacío = todos)"), req: false, min: 0) },
            300 => new() { Num("newRate", L(lng, "New tax rate", "Nueva tasa"), min: 10, max: 80) },
            347 or 349 or 351 or 353 or 355 => new() { Sel("destArmyId", L(lng, "Destination army", "Ejército destino"), ArmyOpts()), Num("amount", L(lng, "Amount", "Cantidad"), min: 1) },
            370 or 375 => new() { Sel("material", L(lng, "Material", "Material"), MaterialOptions(lng)),
                Num("hc", L(lng, "Heavy cavalry", "Caballería pesada"), req: false, min: 0), Num("lc", L(lng, "Light cavalry", "Caballería ligera"), req: false, min: 0),
                Num("hi", L(lng, "Heavy infantry", "Infantería pesada"), req: false, min: 0), Num("li", L(lng, "Light infantry", "Infantería ligera"), req: false, min: 0),
                Num("ar", L(lng, "Archers", "Arqueros"), req: false, min: 0), Num("ma", L(lng, "Men-at-arms", "Hombres de armas"), req: false, min: 0) },
            400 or 404 or 408 or 412 => new() { Num("amount", L(lng, "Troops", "Tropas"), min: 1),
                Sel("weapons", L(lng, "Weapon material (bronze/steel/mithril)", "Armas (bronce/acero/mitril)"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()), Sel("armour", L(lng, "Armour material", "Armadura"), MaterialOptions(lng)) },
            416 or 420 => new() { Num("amount", L(lng, "Troops", "Tropas"), min: 1) },
            425 => new() { Num("hc", L(lng, "Heavy cavalry", "Caballería pesada"), req: false, min: 0), Num("lc", L(lng, "Light cavalry", "Caballería ligera"), req: false, min: 0),
                Num("hi", L(lng, "Heavy infantry", "Infantería pesada"), req: false, min: 0), Num("li", L(lng, "Light infantry", "Infantería ligera"), req: false, min: 0),
                Num("ar", L(lng, "Archers", "Arqueros"), req: false, min: 0), Num("ma", L(lng, "Men-at-arms", "Hombres de armas"), req: false, min: 0) },
            444 => new() { Num("amount", L(lng, "Rank points", "Puntos de rango"), min: 1), Sel("material", L(lng, "Material", "Material"), MaterialOptions(lng).Where(m => m.Value != "wood").ToList()) },
            448 => new() { Num("amount", L(lng, "Rank points", "Puntos de rango"), min: 1), Sel("material", L(lng, "Material", "Material"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()) },
            494 => new() { Sel("level", L(lng, "Fort level (empty = next)", "Nivel de fuerte (vacío = siguiente)"), LevelOptions(lng), req: false) },
            470 or 498 => new() { Num("amount", L(lng, "Amount", "Cantidad"), req: false, min: 0) },
            725 or 728 or 731 or 734 or 737 => new() { Txt("name", L(lng, "Name (5-17 letters, capitalized)", "Nombre (5-17 letras, mayúscula)")),
                Num("command", L(lng, "Command 0-30", "Mando 0-30"), req: false, min: 0, max: 30), Num("agent", L(lng, "Agent 0-30", "Agente 0-30"), req: false, min: 0, max: 30),
                Num("emissary", L(lng, "Emissary 0-30", "Emisario 0-30"), req: false, min: 0, max: 30), Num("mage", L(lng, "Mage 0-30", "Mago 0-30"), req: false, min: 0, max: 30) },
            745 => new() { Txt("name", L(lng, "Company name", "Nombre de compañía")) },
            770 => new() { Txt("name", L(lng, "Army name", "Nombre de ejército")), Num("troops", L(lng, "Troops", "Tropas"), min: 1),
                Sel("troopType", L(lng, "Troop type", "Tipo de tropa"), TroopTypeOptions(lng)), Sel("weapons", L(lng, "Weapons", "Armas"), MaterialOptions(lng).Where(m => m.Value == "bronze" || m.Value == "steel" || m.Value == "mithril").ToList()),
                Sel("armour", L(lng, "Armour", "Armadura"), MaterialOptions(lng)), Num("food", L(lng, "Food units", "Comida"), req: false, min: 0) },
            755 => new() { Sel("commanderId", L(lng, "Company commander", "Comandante de compañía"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.CompanyId != null && !c.IsDead))) },
            765 => new() { Sel("commanderId", L(lng, "New commander", "Nuevo comandante"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId == ctx.Nation.Id && c.CommandSkill > 0 && !c.IsDead))) },
            785 => new() { Sel("commanderId", L(lng, "Force commander", "Comandante de fuerza"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))) },
            780 => new() { Sel("targetId", L(lng, "New commander", "Nuevo comandante"), CharOpts(ownChars)) },
            910 or 915 or 920 or 930 or 475 or 490 or 605 or 665 or 670 or 675 or 680 => new() { Hx("hex", L(lng, "Hex (empty = current location)", "Hex (vacío = actual)"), req: false) },
            610 or 625 or 630 or 635 or 640 or 645 or 650 or 655 or 363 => new()
                { Sel("targetId", L(lng, "Target", "Objetivo"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))) },
            685 => new()
                { Sel("artifactId", L(lng, "Artifact", "Artefacto"), _db.Artifacts.Where(a => (a.HeldByCharacterId != null && a.HeldByCharacterId != ch.Id) || (a.HeldByCharacterId == null && a.LocationHex == ctx.EffLoc)).ToList().Select(a => new OrderFieldOptionDto(a.Id, a.HeldByCharacterId == null ? $"{a.Name} @ {(a.LocationHex ?? "?")}" : a.Name)).ToList()) },
            505 => new() { Sel("targetId", L(lng, "Target character", "Personaje objetivo"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.NationId != ctx.Nation.Id && !c.IsDead))), Num("amount", L(lng, "Bribe gold (min 500)", "Soborno en oro (mín 500)"), req: false, min: 500) },
            552 or 555 => new() { Txt("name", L(lng, "Camp name (empty = nation pool)", "Nombre campamento (vacío = reserva)"), req: false) },
            560 or 565 or 580 or 585 => new() { Hx("hex", L(lng, "Hex (empty = current location)", "Hex (vacío = actual)"), req: false) },
            360 => new() { Multi("artifactId", L(lng, "Artifacts", "Artefactos"), heldArts), Sel("targetId", L(lng, "To character (same hex)", "A personaje (mismo hex)"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead && !c.IsKidnapped))) },
            792 or 796 => new() { Multi("artifactId", L(lng, "Artifacts (1-6)", "Artefactos (1-6)"), heldArts) },
            700 => new() { Multi("spellId", L(lng, "Spells to forget (1-6)", "Hechizos a olvidar (1-6)"), ch.Spells.Where(s => s.IsKnown && !s.IsLost).Select(s => new OrderFieldOptionDto(s.SpellId.ToString(), $"#{s.SpellId} {SpellCatalog.Get(s.SpellId)?.Name ?? "?"}")).ToList()) },
            798 => new() { Num("amount", L(lng, "Transports to pick up", "Transportes a recoger"), min: 1) },
            205 or 945 => new() { Sel("artifactId", L(lng, "Artifact", "Artefacto"), heldArts) },
            805 => new() { Sel("artifactId", L(lng, "Movement artifact", "Artefacto de movimiento"), heldArts), Hx("destination", L(lng, "Destination hex (empty = stay)", "Hex destino (vacío = quedarse)"), req: false) },
            935 => new() { Sel("artifactId", L(lng, "Artifact", "Artefacto"), heldArts), Hx("hex", L(lng, "Hex to scry (empty = here)", "Hex a espiar (vacío = aquí)"), req: false) },
            900 => new() { Sel("artifactId", L(lng, "Artifact (optional)", "Artefacto (opcional)"), _db.Artifacts.Where(a => a.HeldByCharacterId == null).ToList().Select(a => new OrderFieldOptionDto(a.Id, $"{a.Name} @ {a.LocationHex ?? "?"}")).ToList(), req: false) },
            905 => new() { Sel("commanderId", L(lng, "Force commander", "Comandante de fuerza"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => !c.IsDead))), Flag("follow", L(lng, "Follow", "Seguir")), Hx("hex", L(lng, "Hex (empty = force location)", "Hex (vacío = fuerza)"), req: false) },
            940 => LoreRequires(),
            949 => new() { Sel("targetId", L(lng, "Receiving emissary (other nation, same hex)", "Emisario receptor (otra nación, mismo hex)"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.EmissarySkill > 0 && !c.IsDead))) },
            950 => new() { Sel("pcId", L(lng, "New capital (major town/city)", "Nueva capital (villa grande/ciudad)"), ctx.Nation.PopulationCentres.Where(p => p.Size == "major town" || p.Size == "city").Select(p => new OrderFieldOptionDto(p.Id, p.Name)).ToList()) },
            660 => new() { Sel("targetId", L(lng, "Hostage", "Rehén"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => c.IsKidnapped && !c.IsDead))), Num("amount", L(lng, "Ransom gold (empty = 1000)", "Rescate en oro (vacío = 1000)"), req: false, min: 1) },
            500 => new() { Sel("targetId", L(lng, "Target character (emissary/agent, same hex)", "Objetivo (emisario/agente, mismo hex)"), CharOpts(ctx.Game.Nations.SelectMany(n => n.Characters).Where(c => (c.EmissarySkill > 0 || c.AgentSkill > 0) && !c.IsDead))) },
            810 or 820 or 830 or 850 or 860 or 870 => new() { Txt("destination", L(lng, "Destination hex", "Hex destino")), Flag("evasive", L(lng, "Evasive", "Evasivo")) },
            825 => new() { Sel("spellId", L(lng, "Spell", "Hechizo"), SpellOpts(SpellType.Movement)), Txt("destination", L(lng, "Destination hex", "Hex destino")) },
            310 => new() { Sel("product", L(lng, "Product", "Producto"), ProductOptions(lng)), Num("amount", L(lng, "Amount", "Cantidad"), min: 1), Num("price", L(lng, "Bid price (empty = market)", "Puja (vacío = mercado)"), req: false, min: 1) },
            315 or 320 => new() { Sel("product", L(lng, "Product", "Producto"), ProductOptions(lng)), Num("amount", L(lng, "Amount", "Cantidad"), min: 1) },
            325 => new() { Sel("product", L(lng, "Product", "Producto"), ProductOptions(lng)), Num("percentage", L(lng, "Percentage of stock", "Porcentaje de reserva"), min: 1, max: 100) },
            947 or 948 => new() { Sel("resource", L(lng, "Resource", "Recurso"), ProductOptions(lng)), Num("amount", L(lng, "Amount", "Cantidad"), min: 1) },
            _ => new List<OrderFieldSpecDto>()
        };
    }

    private List<string> ValidateEstimateParams(int code, EstimateCtx ctx, List<OrderFieldSpecDto> requires)
    {
        var errors = new List<string>();
        var pars = ctx.Pars;
        var lng = ctx.Lang;
        foreach (var f in requires.Where(f => f.Required))
        {
            if (!pars.TryGetValue(f.Key, out var el) || el.ValueKind == System.Text.Json.JsonValueKind.Null
                || (el.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(el.GetString()))
                || (el.ValueKind == System.Text.Json.JsonValueKind.Array && !el.EnumerateArray().Any()))
                errors.Add(L(lng, "Missing info: ", "Falta info: ") + f.Label);
        }
        string? Str(string k) => ParamStr(pars, k);
        var ch = ctx.Ch;
        var gameChars = ctx.Game.Nations.SelectMany(n => n.Characters).ToList();
        Character? FindChar(string? id) => string.IsNullOrEmpty(id) ? null : gameChars.FirstOrDefault(c => c.Id == id);
        PopulationCentre? PcAt(string? hex) => hex == null ? null :
            ctx.Game.Nations.SelectMany(n => n.PopulationCentres).FirstOrDefault(p => p.LocationHex == hex);
        PopulationCentre? OwnPcAt(string? hex) => hex == null ? null :
            ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == hex);

        if (code is 750 or 760 && ch.CompanyId == null) errors.Add(L(lng, "Not in a company", "Fuera de compañía"));
        if (code == 790 && ch.ArmyId == null) errors.Add(L(lng, "Not in an army", "Fuera del ejército"));
        if (code is 210 or 615 or 620)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add(L(lng, "Target character not found", "Personaje objetivo no encontrado"));
            else
            {
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, $"Target not in the same hex (at {t.LocationHex})", $"El objetivo no está en el mismo hex (en {t.LocationHex})"));
                if (code == 615 && t.NationId == ctx.Nation.Id) errors.Add(L(lng, "Target must be of a different nation", "El objetivo debe ser de otra nación"));
                if (code == 615 && t.IsKidnapped) errors.Add(L(lng, "Target is a hostage", "El objetivo es un rehén"));
                if (code == 620 && (t.IsDead || t.IsKidnapped)) errors.Add(L(lng, "Target cannot be kidnapped", "El objetivo no puede ser secuestrado"));
                if (code == 620 && t.NationId == ctx.Nation.Id) errors.Add(L(lng, "Target must be of a different nation", "El objetivo debe ser de otra nación"));
            }
        }
        if (code is 625 or 630 or 635 or 640 or 645 or 650 or 655)
        {
            var t = FindChar(Str("targetId"));
            if (t == null) errors.Add(L(lng, "Target not found", "Objetivo no encontrado"));
            else
            {
                if (!t.IsKidnapped) errors.Add(L(lng, "Target is not a hostage", "El objetivo no es un rehén"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, $"Target not in the same hex (at {t.LocationHex})", $"El objetivo no está en el mismo hex (en {t.LocationHex})"));
            }
        }
        if (code == 275 && !ctx.Nation.Navies.Any()) errors.Add(L(lng, "No navy", "Sin armada"));
        if (code == 280)
        {
            var cap = ctx.Nation.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
            if (cap == null || cap != ctx.EffLoc) errors.Add(L(lng, "Must be at your own capital", "Debes estar en tu capital"));
            if (!ctx.Nation.Navies.Any()) errors.Add(L(lng, "No navy", "Sin armada"));
        }
        if (code is 300 or 325)
        {
            var cap = ctx.Nation.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
            if (cap == null || cap != ctx.EffLoc) errors.Add(L(lng, "Must be at your own capital", "Debes estar en tu capital"));
        }
        // Prerequisite mirrors (same messages as TurnProcessor resolve).
        var n = ctx.Nation;
        var game = ctx.Game;
        string? S(string k) => ParamStr(pars, k);
        int I(string k, int def = 0) => ParamInt(pars, k, def);
        Character? FC(string? id) => FindChar(id);
        bool SameOrFriendly(string a, string b) => a == b ||
            (game.Nations.FirstOrDefault(x => x.Id == a)?.Relations.FirstOrDefault(r => r.TargetNationId == b)?.Level ?? 0) >= 1;
        bool EnemyAt(string? hex)
        {
            if (hex == null) return false;
            bool Hostile(string a, string b) => game.Nations.FirstOrDefault(x => x.Id == a)?.Relations
                .FirstOrDefault(r => r.TargetNationId == b)?.Level <= -1;
            return game.Nations
                .SelectMany(x => x.Armies.Select(a => new { a.LocationHex, NationId = x.Id })
                    .Concat(x.Navies.Select(v => new { v.LocationHex, NationId = x.Id })))
                .Any(u => u.LocationHex == hex && u.NationId != n.Id
                    && (Hostile(u.NationId, n.Id) || Hostile(n.Id, u.NationId)));
        }
        bool OnLand(string? hex)
        {
            var t = TileAt(game.Id, hex);
            return t == null || (t.Terrain != "water" && t.Terrain != "ocean");
        }
        var capHex = n.PopulationCentres.FirstOrDefault(p => p.IsCapital)?.LocationHex;
        var atCapital = capHex != null && capHex == ctx.EffLoc;
        bool Usable(Artifact a)
        {
            var al = (a.Alignment ?? "").ToLower();
            if (al is "" or "none" or "neutral") return true;
            if (al == "good") return n.Allegiance == "free_peoples";
            if (al == "evil") return n.Allegiance == "dark_servants";
            return true;
        }
        bool CommandsForce(Character c) => c.ArmyId != null || c.CompanyId != null
            || game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id);
        Army? SrcArmy() => S("armyId") != null
            ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
            : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
        int TroopsOf(Army a, string t) => t switch
        {
            "HeavyCavalry" => a.HeavyCavalry, "LightCavalry" => a.LightCavalry,
            "HeavyInfantry" => a.HeavyInfantry, "LightInfantry" => a.LightInfantry,
            "Archers" => a.Archers, "MenAtArms" => a.MenAtArms, _ => 0
        };
        List<string> Ids(string key)
        {
            var ids = new List<string>();
            if (!pars.TryGetValue(key, out var el)) return ids;
            void AddEl(System.Text.Json.JsonElement e)
            {
                if (e.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrEmpty(e.GetString())) ids.Add(e.GetString()!);
                else if (e.ValueKind == System.Text.Json.JsonValueKind.Number && e.TryGetInt32(out var nn)) ids.Add(nn.ToString());
            }
            if (el.ValueKind == System.Text.Json.JsonValueKind.Array) foreach (var e in el.EnumerateArray()) AddEl(e);
            else AddEl(el);
            return ids;
        }

        // 205/805/935/945: held by the character + type + alignment.
        if (code is 205 or 805 or 935 or 945)
        {
            var want = code == 205
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sword", "Weapon", "Bow", "Mace", "Scimitar", "Hammer", "Lance", "Club", "Flail", "Axe", "Bola", "Spear" }
                : code == 805 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Boots" }
                : code == 935 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mirror", "Orb", "Sphere" }
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cloak", "Robes" };
            var kind = code == 205 ? "combat" : code == 805 ? "movement" : code == 935 ? "scrying" : "hiding";
            var aid = S("artifactId");
            var a = aid == null ? null : _db.Artifacts.FirstOrDefault(x => x.Id == aid);
            if (aid != null && a == null) errors.Add(L(lng, "Artifact not found", "Artefacto no encontrado"));
            else if (a != null)
            {
                if (a.HeldByCharacterId != ch.Id) errors.Add(L(lng, "Artifact not held by character", "El personaje no lleva el artefacto"));
                if (!want.Contains(a.Type ?? "")) errors.Add(L(lng, $"{a.Name} is not a {kind} artifact", $"{a.Name} no es un artefacto de {kind}"));
                if (!Usable(a)) errors.Add(L(lng, $"{a.Name} alignment does not match your allegiance", $"El alineamiento de {a.Name} no vale para tu bando"));
            }
        }
        // 347-355: source army + dest same hex + same/friendly.
        if (code is 347 or 349 or 351 or 353 or 355)
        {
            var src = SrcArmy();
            if (src == null) errors.Add(L(lng, "No source army", "Sin ejército origen"));
            else
            {
                var did = S("targetArmyId") ?? S("destArmyId");
                var dest = game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.Id == did);
                if (dest == null) errors.Add(L(lng, "Dest army not found", "Ejército destino no encontrado"));
                else
                {
                    if (dest.LocationHex != src.LocationHex) errors.Add(L(lng, "Both armies must be in the same hex", "Ambos ejércitos en el mismo hex"));
                    if (!SameOrFriendly(n.Id, dest.NationId)) errors.Add(L(lng, "Dest army must be of the same or a friendly nation", "El destino debe ser de tu nación o amiga"));
                }
            }
        }
        if (code == 360)
        {
            if (Ids("artifactId").Count == 0) errors.Add(L(lng, "Missing artifactId(s)", "Faltan artefactos"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || t.IsKidnapped)) errors.Add(L(lng, "Target not found or is a hostage", "Objetivo no encontrado o es rehén"));
            else if (t != null && t.LocationHex != ctx.EffLoc) errors.Add(L(lng, "Both characters must be at the same location", "Ambos personajes en el mismo lugar"));
        }
        if (code == 363)
        {
            var t = FC(S("targetId"));
            if (S("targetId") != null && t == null) errors.Add(L(lng, "Target not found", "Objetivo no encontrado"));
            else
            {
                if (!t.IsKidnapped) errors.Add(L(lng, "Target is not a hostage", "El objetivo no es un rehén"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, "Target must be at the same location", "El objetivo debe estar en el mismo lugar"));
            }
        }
        // 370/375: army + material + troop types present.
        if (code is 370 or 375)
        {
            var army = SrcArmy();
            if (army == null) errors.Add(L(lng, "No army specified", "Sin ejército"));
            else
            {
                var m = (S("material") ?? "").ToLower();
                if (m != "leather" && m != "bronze" && m != "steel" && m != "mithril")
                    errors.Add(L(lng, "Material must be leather, bronze, steel or mithril", "Material: cuero, bronce, acero o mitril"));
                var pairs = new[] { ("hc", "HeavyCavalry"), ("lc", "LightCavalry"), ("hi", "HeavyInfantry"), ("li", "LightInfantry"), ("ar", "Archers"), ("ma", "MenAtArms") };
                var wanted = pairs.Where(p => I(p.Item1) > 0).Select(p => p.Item2).ToList();
                if (wanted.Count == 0) errors.Add(L(lng, "Specify troops per type (hc/lc/hi/li/ar/ma)", "Indica tropas por tipo (hc/lc/hi/li/ar/ma)"));
                var missing = wanted.Where(t => TroopsOf(army, t) <= 0).ToList();
                if (missing.Count > 0) errors.Add(L(lng, $"No {string.Join(", ", missing)} troops in {army.Name}", $"Sin tropas {string.Join(", ", missing)} en {army.Name}"));
            }
        }
        if (code is 400 or 404 or 408 or 412)
        {
            var w = (S("weapons") ?? "").ToLower();
            if (w != "bronze" && w != "steel" && w != "mithril")
                errors.Add(L(lng, "Weapon material must be bronze, steel or mithril", "Armas de bronce, acero o mitril"));
        }
        if (code == 425)
        {
            if (SrcArmy() == null) errors.Add(L(lng, "No army", "Sin ejército"));
            else if (I("hc") + I("lc") + I("hi") + I("li") + I("ar") + I("ma") <= 0)
                errors.Add(L(lng, "No troops retired (amounts per type: hc/lc/hi/li/ar/ma)", "Sin tropas licenciadas (cantidades por tipo)"));
        }
        if (code == 444 && new[] { "leather", "bronze", "steel", "mithril" }.All(m => m != (S("material") ?? "").ToLower()))
            errors.Add(L(lng, "Material must be leather, bronze, steel or mithril", "Material: cuero, bronce, acero o mitril"));
        if (code == 448 && new[] { "bronze", "steel", "mithril" }.All(m => m != (S("material") ?? "").ToLower()))
            errors.Add(L(lng, "Material must be bronze, steel or mithril", "Material: bronce, acero o mitril"));
        // 520/525 influence.
        if (code == 520)
        {
            if (OwnPcAt(ctx.EffLoc) == null) errors.Add(L(lng, "Must be at one of your population centres", "Debes estar en un centro propio"));
            if (ch.EmissarySkill <= 0) errors.Add(L(lng, "Needs emissary skill", "Requiere emisario"));
        }
        if (code == 525)
        {
            if (ch.EmissarySkill <= 0) errors.Add(L(lng, "Needs emissary skill", "Requiere emisario"));
            var pc = PcAt(ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "No population centre here", "Aquí no hay centro de población"));
            else
            {
                if (pc.NationId == n.Id) errors.Add(L(lng, "Use 520 on your own centres", "Usa 520 en tus centros"));
                if (pc.IsHidden) errors.Add(L(lng, "No visible population centre here", "Aquí no hay centro visible"));
                var back = game.Nations.FirstOrDefault(x => x.Id == pc.NationId)?.Relations
                    .FirstOrDefault(r => r.TargetNationId == n.Id)?.Level <= -1;
                if (back) errors.Add(L(lng, "Enemy forces present", "Hay fuerzas enemigas"));
            }
        }
        // 530/535/550/552/555 constructions.
        if (code is 530 or 535 or 550)
        {
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "Must be at one of your population centres", "Debes estar en un centro propio"));
            else
            {
                if (code == 530 && (!pc.HasHarbour || pc.HasPort)) errors.Add(L(lng, $"{pc.Name} needs a harbour (not yet a port)", $"{pc.Name} necesita fondeadero (aún no puerto)"));
                if (code == 530 && pc.Size != "major town" && pc.Size != "city") errors.Add(L(lng, "Only major towns and cities can have ports", "Solo villas grandes y ciudades tienen puerto"));
                if (code == 535 && (pc.HasHarbour || pc.HasPort)) errors.Add(L(lng, $"{pc.Name} already has a harbour or port", $"{pc.Name} ya tiene fondeadero o puerto"));
                if (code == 535 && pc.Size != "town" && pc.Size != "major town" && pc.Size != "city") errors.Add(L(lng, "Only towns and larger can have harbours", "Solo villas o más tienen fondeadero"));
                if (code == 550 && (pc.Size == "city" || pc.Size == "citadel")) errors.Add(L(lng, $"{pc.Name} cannot be improved further", $"{pc.Name} no se puede mejorar más"));
            }
            if (EnemyAt(ctx.EffLoc)) errors.Add(L(lng, "Enemy forces present", "Hay fuerzas enemigas"));
        }
        if (code is 552 or 555)
        {
            if (!OnLand(ctx.EffLoc)) errors.Add(L(lng, "Camps need a land hex", "El campamento requiere tierra"));
            if (OwnPcAt(ctx.EffLoc) != null) errors.Add(L(lng, "Already have a population centre at this hex", "Ya tienes un centro en este hex"));
            if (EnemyAt(ctx.EffLoc)) errors.Add(L(lng, "Enemy forces present", "Hay fuerzas enemigas"));
        }
        if (code == 565)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "No population centre at location", "Sin centro de población aquí"));
            else
            {
                if (pc.NationId != n.Id) errors.Add(L(lng, "Can only reduce your own population centres", "Solo puedes reducir tus centros"));
                if (pc.Size.ToLower() == "camp") errors.Add(L(lng, "Camps cannot be reduced further (abandon them)", "Los campamentos no se reducen (abandónalos)"));
            }
        }
        if (code == 605)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc != null && pc.NationId != n.Id && pc.IsHidden) errors.Add(L(lng, "No visible population centre here", "Aquí no hay centro visible"));
        }
        if (code == 660)
        {
            if (!atCapital) errors.Add(L(lng, "Must be at your own capital", "Debes estar en tu capital"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || !t.IsKidnapped)) errors.Add(L(lng, "Target not found or not kidnapped", "Objetivo no encontrado o no secuestrado"));
        }
        // 670/675/680 sabotage.
        if (code is 670 or 675 or 680)
        {
            var pc = PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "No population centre", "Sin centro de población"));
            else
            {
                if (pc.NationId == n.Id) errors.Add(L(lng, "Target must be of a different nation", "El objetivo debe ser de otra nación"));
                if (pc.IsHidden) errors.Add(L(lng, "No visible population centre here", "Aquí no hay centro visible"));
                if (code == 670 && string.IsNullOrEmpty(pc.Fortification)) errors.Add(L(lng, $"{pc.Name} has no fortifications", $"{pc.Name} no tiene fortificaciones"));
                if (code == 675 && !pc.HasHarbour && !pc.HasPort) errors.Add(L(lng, $"{pc.Name} has no harbour or port", $"{pc.Name} no tiene fondeadero ni puerto"));
                if (code == 680)
                {
                    var st = (S("store") ?? "timber").ToLower();
                    if (st != "timber" && st != "food" && st != "mounts" && st != "leather" && st != "bronze" && st != "steel" && st != "mithril")
                        errors.Add(L(lng, "Store must be timber, food, mounts, leather, bronze, steel or mithril (not gold)", "Reserva de madera, comida, monturas, cuero, bronce, acero o mitril (no oro)"));
                }
            }
        }
        if (code == 685)
        {
            var aid685 = S("artifactId");
            var a = aid685 == null ? null : _db.Artifacts.FirstOrDefault(x => x.Id == aid685);
            if (aid685 != null && a == null) errors.Add(L(lng, "Artifact not found", "Artefacto no encontrado"));
            else if (a.HeldByCharacterId == ch.Id) errors.Add(L(lng, "You already hold it", "Ya lo tienes"));
            else if (a.HeldByCharacterId != null)
            {
                var holder = gameChars.FirstOrDefault(c => c.Id == a.HeldByCharacterId);
                if (holder == null) errors.Add(L(lng, "Holder not found", "Portador no encontrado"));
                else
                {
                    if (holder.NationId == n.Id) errors.Add(L(lng, "Target must belong to another nation", "El objetivo debe ser de otra nación"));
                    if (holder.LocationHex != ctx.EffLoc) errors.Add(L(lng, "Artifact must be at the same hex", "El artefacto debe estar en el mismo hex"));
                }
            }
            else
            {
                if (a.LocationHex != ctx.EffLoc) errors.Add(L(lng, "Artifact must be at the same hex", "El artefacto debe estar en el mismo hex"));
                else
                {
                    var pc = PcAt(a.LocationHex);
                    if (pc != null && (pc.IsHidden || pc.NationId == n.Id))
                        errors.Add(L(lng, "Artifact must lie in a visible foreign population centre", "El artefacto debe estar en un centro extranjero visible"));
                }
            }
        }
        if (code == 690)
        {
            var victim = game.Nations.FirstOrDefault(x => x.Id == S("nationId"));
            if (victim == null) errors.Add(L(lng, "Victim nation not found", "Nación víctima no encontrada"));
            else
            {
                if (victim.Id == n.Id) errors.Add(L(lng, "Target must be of a different nation", "El objetivo debe ser de otra nación"));
                var pc = game.Nations.SelectMany(x => x.PopulationCentres)
                    .FirstOrDefault(x => x.LocationHex == ctx.EffLoc && x.NationId == victim.Id);
                if (pc == null || pc.IsHidden) errors.Add(L(lng, "Need a visible foreign population centre at your hex", "Necesitas un centro extranjero visible en tu hex"));
            }
        }
        if (code is 792 or 796)
        {
            var c = Ids("artifactId").Count;
            if (c < 1 || c > 6) errors.Add(L(lng, "Give 1-6 artifactId(s)", "Indica 1-6 artefactos"));
        }
        if (code == 700)
        {
            var c = Ids("spellId").Count;
            if (c < 1 || c > 6) errors.Add(L(lng, "Give 1-6 spellId(s) to forget", "Indica 1-6 hechizos a olvidar"));
        }
        if (code == 705)
        {
            if (OwnPcAt(ctx.EffLoc) == null) errors.Add(L(lng, "Must be at one of your population centres to research", "Debes estar en un centro propio para investigar"));
            if (ch.Spells.Count(s => s.IsKnown && !s.IsLost) >= 15) errors.Add(L(lng, "Already knows 15 spells", "Ya conoce 15 hechizos"));
            if (pars.TryGetValue("spellId", out var rsEl) && rsEl.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var sd = SpellCatalog.Get(rsEl.GetInt32());
                if (sd == null) errors.Add(L(lng, "Unknown spell", "Hechizo desconocido"));
                else if (sd.IsLost && !NationAbilities.CanLearnLostSpell(n.Name, sd.Id))
                    errors.Add(L(lng, $"Lost spell {sd.Name} is not available to your nation", $"El hechizo perdido {sd.Name} no está disponible para tu nación"));
            }
        }
        if (code == 710 && OwnPcAt(ctx.EffLoc) == null)
            errors.Add(L(lng, "Must be at one of your population centres to train", "Debes estar en un centro propio para entrenar"));
        if (code is 725 or 728 or 731 or 734 or 737)
        {
            var nm = (S("name") ?? "").Trim();
            if (nm.Length < 5 || nm.Length > 17) errors.Add(L(lng, "Name must be 5-17 letters", "El nombre debe tener 5-17 letras"));
            if (!atCapital) errors.Add(L(lng, "Must be at your own capital", "Debes estar en tu capital"));
        }
        if (code == 740 && ch.IsKidnapped) errors.Add(L(lng, "Cannot retire a hostage", "No puedes retirar un rehén"));
        if (code == 745)
        {
            if (CommandsForce(ch)) errors.Add(L(lng, "Character already commands a force", "El personaje ya manda una fuerza"));
            if (!OnLand(ctx.EffLoc)) errors.Add(L(lng, "Must be on land", "Debes estar en tierra"));
        }
        if (code == 755)
        {
            var c = FC(S("commanderId"));
            if (S("commanderId") != null && (c == null || c.CompanyId == null)) errors.Add(L(lng, "Commander has no company", "El comandante no tiene compañía"));
            else
            {
                var comp = _db.Companies.Find(c.CompanyId);
                if (comp == null) errors.Add(L(lng, "Company not found", "Compañía no encontrada"));
                else
                {
                    if (_db.Characters.Count(x => x.CompanyId == comp.Id && !x.IsDead) >= 9)
                        errors.Add(L(lng, $"Company '{comp.Name}' is full (9 members)", $"La compañía '{comp.Name}' está llena (9 miembros)"));
                    if (comp.NationId != n.Id && !SameOrFriendly(n.Id, comp.NationId))
                        errors.Add(L(lng, "Company must be of the same or a friendly nation", "La compañía debe ser de tu nación o amiga"));
                }
            }
        }
        if (code == 765)
        {
            var army = SrcArmy();
            if (army == null) errors.Add(L(lng, "No army to split", "Sin ejército que dividir"));
            var c = FC(S("commanderId"));
            if (S("commanderId") != null && (c == null || c.IsDead)) errors.Add(L(lng, "New commander not found", "Nuevo comandante no encontrado"));
            else
            {
                if (c.NationId != n.Id) errors.Add(L(lng, "New commander must be of the same nation", "El nuevo comandante debe ser de tu nación"));
                if (c.CommandSkill <= 0) errors.Add(L(lng, "New commander needs command skill", "El nuevo comandante necesita mando"));
                if (c.ArmyId != null || c.CompanyId != null
                    || game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == c.Id))
                    errors.Add(L(lng, $"{c.Name} already commands a force", $"{c.Name} ya manda una fuerza"));
                if (army != null && c.LocationHex != army.LocationHex)
                    errors.Add(L(lng, "New commander must be at the same hex", "El nuevo comandante debe estar en el mismo hex"));
            }
        }
        if (code == 770)
        {
            if (CommandsForce(ch)) errors.Add(L(lng, "Character already commands a force", "El personaje ya manda una fuerza"));
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null || pc.IsSieged) errors.Add(L(lng, "Must be at one of your non-sieged population centres", "Debes estar en un centro propio no asediado"));
            var tt = S("troopType") ?? "MenAtArms";
            var shorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" }, { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" } };
            if (shorts.TryGetValue(tt, out var fullT)) tt = fullT;
            if (tt != "HeavyCavalry" && tt != "LightCavalry" && tt != "HeavyInfantry" && tt != "LightInfantry" && tt != "Archers" && tt != "MenAtArms")
                errors.Add(L(lng, "Troop type must be hc, lc, hi, li, ar or ma", "Tipo de tropa: hc, lc, hi, li, ar o ma"));
            var w = (S("weapons") ?? "bronze").ToLower();
            if (w != "bronze" && w != "steel" && w != "mithril") errors.Add(L(lng, "Weapon material must be bronze, steel or mithril", "Armas de bronce, acero o mitril"));
            var ar2 = (S("armour") ?? "leather").ToLower();
            if (ar2 != "leather" && ar2 != "bronze" && ar2 != "steel" && ar2 != "mithril")
                errors.Add(L(lng, "Armour material must be leather, bronze, steel or mithril", "Armadura de cuero, bronce, acero o mitril"));
        }
        if (code == 775)
        {
            var army = S("armyId") != null
                ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
                : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
            if (army == null) errors.Add(L(lng, "No army to disband", "Sin ejército que disolver"));
        }
        if (code == 780)
        {
            var army = S("armyId") != null
                ? n.Armies.FirstOrDefault(a => a.Id == S("armyId"))
                : n.Armies.FirstOrDefault(a => a.Id == ch.ArmyId);
            if (army == null) errors.Add(L(lng, "No army to transfer command of", "Sin ejército que transferir"));
            var t = FC(S("targetId"));
            if (S("targetId") != null && t == null) errors.Add(L(lng, "Target commander not found", "Comandante objetivo no encontrado"));
            else if (t != null)
            {
                if (t.NationId != n.Id) errors.Add(L(lng, "New commander must be of the same nation", "El nuevo comandante debe ser de tu nación"));
                if (t.CommandSkill <= 0) errors.Add(L(lng, "New commander needs command skill", "El nuevo comandante necesita mando"));
            }
        }
        if (code == 785)
        {
            if (CommandsForce(ch)) errors.Add(L(lng, "Character already commands a force", "El personaje ya manda una fuerza"));
            var boss = FC(S("commanderId"));
            if (S("commanderId") != null && (boss == null || boss.IsDead)) errors.Add(L(lng, "Commander not found", "Comandante no encontrado"));
            else
            {
                if (boss.NationId != n.Id) errors.Add(L(lng, "Can only join a force of your own nation", "Solo puedes unirte a una fuerza de tu nación"));
                var fa = game.Nations.SelectMany(x => x.Armies).FirstOrDefault(a => a.CommanderId == boss.Id);
                var fv = fa == null ? game.Nations.SelectMany(x => x.Navies).FirstOrDefault(v => v.CommanderId == boss.Id) : null;
                if (fa == null && fv == null) errors.Add(L(lng, $"{boss.Name} commands no force", $"{boss.Name} no manda ninguna fuerza"));
                else if ((fa?.LocationHex ?? fv!.LocationHex) != ctx.EffLoc)
                    errors.Add(L(lng, "Force to join must be at the same hex", "La fuerza debe estar en el mismo hex"));
            }
        }
        if (code == 905)
        {
            var boss = FC(S("commanderId"));
            if (S("commanderId") != null && (boss == null || boss.IsDead)) errors.Add(L(lng, "Commander not found", "Comandante no encontrado"));
            else
            {
                var fa = game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id);
                var fv = game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id);
                if (!fa && !fv) errors.Add(L(lng, $"{boss.Name} commands no force", $"{boss.Name} no manda ninguna fuerza"));
            }
        }
        if (code is 910 or 915 or 925 or 930 && !OnLand(ctx.EffLoc))
            errors.Add(L(lng, "Must be on land", "Debes estar en tierra"));
        if (code == 940)
        {
            if (S("targetId") is { } t1 && FC(t1) == null) errors.Add(L(lng, "Target character not found", "Personaje objetivo no encontrado"));
            if (S("nationId") is { } n1 && !game.Nations.Any(x => x.Id == n1)) errors.Add(L(lng, "Nation not found", "Nación no encontrada"));
            if (S("artifactId") is { } a1 && !_db.Artifacts.Any(x => x.Id == a1)) errors.Add(L(lng, "Artifact not found", "Artefacto no encontrado"));
            if (S("commanderId") is { } c1)
            {
                var boss = FC(c1);
                if (boss == null || boss.IsDead) errors.Add(L(lng, "Commander not found", "Comandante no encontrado"));
                else if (!game.Nations.SelectMany(x => x.Armies).Any(a => a.CommanderId == boss.Id)
                    && !game.Nations.SelectMany(x => x.Navies).Any(v => v.CommanderId == boss.Id))
                    errors.Add(L(lng, $"{boss.Name} commands no force", $"{boss.Name} no manda ninguna fuerza"));
            }
            if (S("allegiance") is { } al && al != "free_peoples" && al != "dark_servants" && al != "neutral")
                errors.Add(L(lng, "Allegiance must be free_peoples, dark_servants or neutral", "Lealtad: free_peoples, dark_servants o neutral"));
        }
        if (code == 949)
        {
            if (ch.EmissarySkill <= 0) errors.Add(L(lng, "Needs emissary skill", "Requiere emisario"));
            var pc = OwnPcAt(ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "No population centre", "Sin centro de población"));
            else
            {
                if (pc.IsHidden) errors.Add(L(lng, "Population centre is hidden", "El centro está oculto"));
                if (pc.IsCapital) errors.Add(L(lng, "Cannot transfer the capital", "No puedes transferir la capital"));
            }
            var t = FC(S("targetId"));
            if (S("targetId") != null && (t == null || t.IsDead)) errors.Add(L(lng, "Target not found", "Objetivo no encontrado"));
            else if (t != null)
            {
                if (t.IsKidnapped) errors.Add(L(lng, "Target is a hostage", "El objetivo es un rehén"));
                if (t.EmissarySkill <= 0) errors.Add(L(lng, "Target needs emissary skill", "El objetivo necesita emisario"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, "Target must be at the same location", "El objetivo debe estar en el mismo lugar"));
                if (t.NationId == n.Id) errors.Add(L(lng, "Target must be of another nation", "El objetivo debe ser de otra nación"));
                var fwd = n.Relations.FirstOrDefault(r => r.TargetNationId == t.NationId);
                if ((fwd?.Level ?? 0) <= -1) errors.Add(L(lng, "Nations are enemies", "Las naciones son enemigas"));
            }
        }
        if (code == 950)
        {
            if (ctx.EffLoc != capHex) errors.Add(L(lng, "Must be at your current capital", "Debes estar en tu capital actual"));
            PopulationCentre? pc = S("pcId") != null
                ? game.Nations.SelectMany(x => x.PopulationCentres).FirstOrDefault(p => p.Id == S("pcId"))
                : PcAt(S("hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add("No population centre");
            else
            {
                if (pc.NationId != n.Id) errors.Add("New capital must be owned by your nation");
                if (pc.IsSieged) errors.Add("Capital and new capital must not be under siege");
                var cur = n.PopulationCentres.FirstOrDefault(x => x.IsCapital);
                if (cur != null && cur.IsSieged) errors.Add(L(lng, "Capital and new capital must not be under siege", "Ni la capital ni la nueva deben estar asediadas"));
                if (pc.Size != "major town" && pc.Size != "city") errors.Add(L(lng, "New capital must be a major town or city", "La nueva capital debe ser villa grande o ciudad"));
            }
        }
        if (code == 990)
        {
            if (n.Allegiance == "neutral") errors.Add(L(lng, "Neutral nations cannot wield the One Ring", "Las naciones neutrales no pueden blandir el Anillo Único"));
            if (CommandsForce(ch)) errors.Add(L(lng, "Bearer must travel alone (no army, company or navy)", "El portador debe viajar solo (sin ejército, compañía ni armada)"));
            if ((ctx.EffLoc ?? "") != "34,23") errors.Add(L(lng, "The bearer must be at Mount Doom (34,23)", "El portador debe estar en el Monte del Destino (34,23)"));
            var ring = _db.Artifacts.FirstOrDefault(a => a.HeldByCharacterId == ch.Id
                && (a.Id == "14" || (a.Name ?? "").Contains("One Ring", StringComparison.OrdinalIgnoreCase)));
            if (ring == null) errors.Add(L(lng, "Bearer must possess artifact #14 The One Ring", "El portador debe llevar el artefacto #14 El Anillo Único"));
        }
        if (code is 685)
        {
            var a = Str("artifactId");
            if (a != null && !_db.Artifacts.Any(x => x.Id == a)) errors.Add(L(lng, "Artifact not found", "Artefacto no encontrado"));
        }
        if (code is 690)
        {
            if (!ctx.Game.Nations.Any(n => n.Id == Str("nationId"))) errors.Add(L(lng, "Victim nation not found", "Nación víctima no encontrada"));
        }
        if (code is 180 or 185 && !ctx.Game.Nations.Any(n => n.Id == Str("nationId")))
            errors.Add(L(lng, "Nation not found", "Nación no encontrada"));
        if (code == 175)
        {
            var al = (Str("allegiance") ?? "").ToLower();
            if (al != "free_peoples" && al != "dark_servants" && al != "neutral")
                errors.Add(L(lng, "Allegiance must be free_peoples, dark_servants or neutral", "Lealtad: free_peoples, dark_servants o neutral"));
        }
        if (code is 120 or 330 or 940 or 225 or 825)
        {
            var want = code == 120 ? SpellType.Heal : code == 330 ? SpellType.Conjuring
                : code == 940 ? SpellType.Lore : code == 825 ? SpellType.Movement : SpellType.Combat;
            var sid = ParamInt(pars, "spellId", -1);
            var def = SpellCatalog.Get(sid);
            if (def == null || def.Type != want || def.IsLost
                || !ch.Spells.Any(s => s.SpellId == sid && s.IsKnown && !s.IsLost))
                errors.Add(L(lng, $"Info must contain a valid known {want.ToString().ToLower()} spell", $"Info debe traer un hechizo válido conocido de {L(lng, want.ToString().ToLower(), SpellTypeEs(want))}"));
        }
        if (code is 700 or 705 && pars.TryGetValue("spellId", out var sidEl)
            && sidEl.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            var sd = SpellCatalog.Get(sidEl.GetInt32());
            if (sd == null) errors.Add(L(lng, "Unknown spell", "Hechizo desconocido"));
            else if (code == 705 && sd.IsLost && !NationAbilities.CanLearnLostSpell(ctx.Nation.Name, sd.Id))
                errors.Add(L(lng, $"Lost spell {sd.Name} is not available to your nation", $"El hechizo perdido {sd.Name} no está disponible para tu nación"));
        }
        if (code is 755 && Str("companyId") != null)
        {
            var cid = Str("companyId");
            if (!_db.Companies.Any(c => c.Id == cid && c.NationId == ctx.Nation.Id))
                errors.Add(L(lng, "Company not found", "Compañía no encontrada"));
        }
        if (code == 560 && !ctx.Nation.PopulationCentres.Any(p => p.LocationHex == ctx.EffLoc && p.Size == "camp"))
            errors.Add(L(lng, "No camp of yours here", "Aquí no hay campamento tuyo"));
        if (code is 310 or 315 or 320 or 325)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "product"), out _))
                errors.Add(L(lng, "Product must be timber, leather, bronze, steel, mithril, mounts or food", "Producto: madera, cuero, bronce, acero, mitril, monturas o comida"));
        }
        if (code is 947 or 948)
        {
            if (!TurnProcessor.IsMarketProductName(ParamStr(pars, "resource"), out _))
                errors.Add(L(lng, "Resource must be timber, leather, bronze, steel, mithril, mounts or food", "Recurso: madera, cuero, bronce, acero, mitril, monturas o comida"));
        }
        if (code is 475 or 490 or 665)
        {
            var tile = TileAt(gameId: ctx.Game.Id, hex: ParamStr(pars, "hex") ?? ctx.EffLoc ?? "");
            if (tile == null) errors.Add(L(lng, "No such hex", "Ese hex no existe"));
            else if (code == 475 && !tile.HasBridge) errors.Add(L(lng, $"No bridge at {tile.Q},{tile.R}", $"Sin puente en {tile.Q},{tile.R}"));
            else if (code == 665 && !tile.HasBridge) errors.Add(L(lng, $"No bridge at {tile.Q},{tile.R}", $"Sin puente en {tile.Q},{tile.R}"));
            else if (code == 490 && !tile.HasMajorRiver && !tile.HasMinorRiver) errors.Add(L(lng, "No river here", "Aquí no hay río"));
            else if (code == 490 && (tile.HasBridge || tile.HasFord)) errors.Add(L(lng, "A ford or bridge already exists here", "Aquí ya hay vado o puente"));
        }
        if (code == 494)
        {
            var pc = ParamStr(pars, "pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == ParamStr(pars, "pcId"))
                : OwnPcAt(ParamStr(pars, "hex") ?? ctx.EffLoc);
            if (pc == null) errors.Add(L(lng, "Must target one of your population centres", "Debes apuntar a un centro propio"));
        }
        if (code == 500)
        {
            if (ch.EmissarySkill <= 0) errors.Add(L(lng, "Needs emissary skill", "Requiere emisario"));
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && (t == null || t.IsDead)) errors.Add(L(lng, "Target character not found", "Personaje objetivo no encontrado"));
            else if (t != null)
            {
                if (t.EmissarySkill <= 0 && t.AgentSkill <= 0) errors.Add(L(lng, "Target must have emissary or agent skill", "El objetivo debe tener emisario o agente"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, $"Target not in the same hex (at {t.LocationHex})", $"El objetivo no está en el mismo hex (en {t.LocationHex})"));
                if (t.NationId == ctx.Nation.Id) errors.Add(L(lng, "Cannot plant a double agent in your own nation", "No puedes plantar un agente doble en tu nación"));
            }
        }
        if (code == 505)
        {
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && t == null) errors.Add(L(lng, "Target not found", "Objetivo no encontrado"));
            else if (t != null)
            {
                if (t.NationId == ctx.Nation.Id) errors.Add(L(lng, "Cannot bribe your own character", "No puedes sobornar a tu personaje"));
                if (t.IsKidnapped) errors.Add(L(lng, "Cannot bribe a hostage", "No puedes sobornar a un rehén"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, $"Target not in the same hex (at {t.LocationHex})", $"El objetivo no está en el mismo hex (en {t.LocationHex})"));
            }
            if (ch.EmissarySkill <= 0) errors.Add(L(lng, "Needs emissary skill", "Requiere emisario"));
        }
        if (code == 610)
        {
            var t = FindChar(Str("targetId"));
            if (Str("targetId") != null && (t == null || t.IsDead)) errors.Add(L(lng, "Target not found", "Objetivo no encontrado"));
            else if (t != null)
            {
                if (t.Id == ch.Id) errors.Add(L(lng, "Cannot guard yourself", "No puedes protegerte a ti mismo"));
                if (t.LocationHex != ctx.EffLoc) errors.Add(L(lng, $"Target not in the same hex (at {t.LocationHex})", $"El objetivo no está en el mismo hex (en {t.LocationHex})"));
            }
        }
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
        var lng = ctx.Lang;
        string? Str(string k) => ParamStr(pars, k);
        if (code == 494)
        {
            var pc = Str("pcId") != null
                ? ctx.Nation.PopulationCentres.FirstOrDefault(p => p.Id == Str("pcId"))
                : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == (Str("hex") ?? ctx.EffLoc));
            if (pc != null && used.FortifiedPcs.Contains(pc.Id))
                errors.Add(L(lng, $"{pc.Name} is already fortified by another order this turn", $"{pc.Name} ya lo fortifica otra orden este turno"));
        }
        if (code == 500 && Str("targetNationId") is { } tn && used.DoubleAgentNations.Contains(tn))
            errors.Add(L(lng, "Another order already recruits a double agent there", "Otra orden ya recluta un agente doble allí"));
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
                errors.Add(L(lng, $"Target already challenged by higher challenge rank ({otherMax} vs yours {ctx.Ch.ChallengeRank})", $"El objetivo ya lo desafía un rango mayor ({otherMax} vs tu {ctx.Ch.ChallengeRank})"));
            else
                warnings.Add(L(lng, "Target already challenged by another character (only the highest CR fights)", "Otro personaje ya desafía al objetivo (solo lucha el CR mayor)"));
        }
        if (code == 505 && Str("targetId") is { } tb && used.BribedTargets.Contains(tb))
            warnings.Add(L(lng, "Target already bribed by another character (first attempt decides)", "Otro personaje ya soborna al objetivo (decide el primer intento)"));
        if ((code == 360 || code == 792) && ParamStr(pars, "artifactId") is { } ar && used.MovedArtifacts.Contains(ar))
            warnings.Add(L(lng, "Artifact already moved by another pending order", "Otra orden pendiente ya mueve el artefacto"));
        if ((code is 320 or 325) && used.SellGoldUsed > 0)
            warnings.Add(L(lng, $"{used.SellGoldUsed} gold of sell cap already used by other orders", $"{used.SellGoldUsed} de oro del tope ya usados por otras órdenes"));
        if (code is 400 or 404 or 408 or 412 or 416 or 420)
        {
            var army = ParamStr(pars, "armyId") != null
                ? ctx.Nation.Armies.FirstOrDefault(a => a.Id == ParamStr(pars, "armyId"))
                : ctx.Nation.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId);
            var pc = army == null ? null : ctx.Nation.PopulationCentres.FirstOrDefault(p => p.LocationHex == army.LocationHex);
            if (pc != null)
            {
                var left = TurnProcessor.RecruitCapacity(pc.Size) - used.RecruitsByPc.GetValueOrDefault(pc.Id);
                var want = ParamInt(pars, "amount", left);
                if (want > left)
                    errors.Add(L(lng, $"Only {Math.Max(0, left)} recruits left at {pc.Name} (other orders use {used.RecruitsByPc.GetValueOrDefault(pc.Id)})", $"Solo quedan {Math.Max(0, left)} reclutas en {pc.Name} (otras órdenes usan {used.RecruitsByPc.GetValueOrDefault(pc.Id)})"));
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
            : n.Armies.FirstOrDefault(a => a.Id == ctx.Ch.ArmyId);
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
                var asked = ParamInt(pars, "hc") + ParamInt(pars, "lc") + ParamInt(pars, "hi")
                    + ParamInt(pars, "li") + ParamInt(pars, "ar") + ParamInt(pars, "ma");
                var amount = Math.Min(asked <= 0 ? present : asked, present);
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
            case 770:
            {
                costs["gold"] = NationAbilities.HireArmyCost(n.Name);
                var tnum = Math.Max(0, ParamInt(pars, "troops", 0));
                if (tnum > 0)
                {
                    var ttype = (ParamStr(pars, "troopType") ?? "MenAtArms");
                    var sh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        { { "hc", "HeavyCavalry" }, { "lc", "LightCavalry" }, { "hi", "HeavyInfantry" }, { "li", "LightInfantry" }, { "ar", "Archers" }, { "ma", "MenAtArms" } };
                    if (sh.TryGetValue(ttype, out var fullT2)) ttype = fullT2;
                    var matU = Math.Max(1, (tnum + 99) / 100);
                    var food = Math.Max(0, ParamInt(pars, "food", 0));
                    if (food > 0) costs["food"] = food;
                    if (ttype is "HeavyCavalry" or "LightCavalry") costs["mounts"] = tnum;
                    costs[(ParamStr(pars, "weapons") ?? "bronze").ToLower()] = matU;
                    costs[(ParamStr(pars, "armour") ?? "leather").ToLower()] = matU;
                }
                break;
            }
            case 950: costs["gold"] = 25000; break;
            case 505: costs["gold"] = Math.Max(500, ParamInt(pars, "amount", 500)); break;
            case 660: costs["gold"] = Math.Max(1000, ParamInt(pars, "amount", 1000)); break;
            case 725: costs["gold"] = 10000; break;
            case 728 or 731 or 734 or 737: costs["gold"] = 5000; break;
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
        if (ctx.Lang == "es")
        {
            var es = new Dictionary<string, int>();
            foreach (var kv in costs) es[CostKey(ctx.Lang, kv.Key)] = kv.Value;
            return es;
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
