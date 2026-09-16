using System.Security.Claims;
using System.Text.Json;
using MEPBMmanager.Api.Orders;
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
    public async Task<IActionResult> List(string gameId, [FromQuery] string? nationId = null, [FromQuery] string? lang = null)
    {
        var lng = OrderTexts.Norm(lang);
        var scope = await ResolveListScope(gameId, nationId);
        if (scope == null)
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });

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
        var lng = OrderTexts.Norm(lang);
        var nationId = await ResolveScopeNation(gameId, request.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = OrderTexts.Get(lng, "err.no-turn-accepting-orders") });

        var character = await _db.Characters
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.NationId == nationId);

        if (character == null)
            return NotFound(new { error = OrderTexts.Get(lng, "err.character-not-found") });

        if (character.IsDead || character.IsKidnapped)
            return BadRequest(new { error = OrderTexts.Get(lng, "err.character-cannot-act") });

        if (!IsValidOrderCode(request.Code))
            return BadRequest(new { error = OrderTexts.Get(lng, "err.invalid-order-code") });

        var pendingCount = await _db.Orders.CountAsync(o => o.GameId == gameId
            && o.TurnId == currentTurn.Id && o.CharacterId == request.CharacterId && o.Status == "pending");
        if (pendingCount >= 2)
            return BadRequest(new { error = OrderTexts.Get(lng, "err.character-already-has-2-orders-this-turn") });

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
    public async Task<IActionResult> Cancel(string gameId, string orderId, [FromQuery] string? lang = null)
    {
        var lng = OrderTexts.Norm(lang);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var role = User.FindFirstValue(ClaimTypes.Role);
        var playerNation = await GetPlayerNationId(gameId);

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.GameId == gameId && o.Status == "pending");

        if (order == null)
            return NotFound(new { error = OrderTexts.Get(lng, "err.order-not-found-or-cannot-be-cancelled") });

        if (order.NationId != playerNation && !await IsStaff(gameId, userId, role))
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Order cancelled" });
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> Validate(string gameId, [FromQuery] string? nationId = null, [FromQuery] string? lang = null)
    {
        var lng = OrderTexts.Norm(lang);
        var scope = await ResolveListScope(gameId, nationId);
        if (scope == null)
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = OrderTexts.Get(lng, "err.no-turn-accepting-orders") });

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
                result.Errors.Add(OrderTexts.Get(lng, "err.character-is-dead"));
            }
            if (order.Character.IsKidnapped)
            {
                result.Valid = false;
                result.Errors.Add(OrderTexts.Get(lng, "err.character-is-kidnapped"));
            }
        }

        return Ok(new { results = validationResults });
    }

    // —— Eligibility: which orders the character can take now ——
    [HttpGet("eligible")]
    [Authorize]
    public async Task<IActionResult> Eligible(string gameId, [FromQuery] string characterId, [FromQuery] string? lang = null)
    {
        var lng = OrderTexts.Norm(lang);
        var nationId = await ResolveScopeNation(gameId, characterId);
        if (nationId == null)
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });
        try
        {
            return Ok(await new OrderEstimateService(_db).EligibleAsync(gameId, characterId, lng, nationId));
        }
        catch (OrderRequestException ex)
        {
            return StatusCode(ex.Status, new { error = ex.Message });
        }
    }

    [HttpPost("estimate")]
    [Authorize]
    public async Task<IActionResult> Estimate(string gameId, [FromBody] EstimateOrderRequest req, [FromQuery] string? lang = null)
    {
        var lng = OrderTexts.Norm(lang);
        var nationId = await ResolveScopeNation(gameId, req.CharacterId);
        if (nationId == null)
            return StatusCode(403, new { error = OrderTexts.Get(lng, "err.not-a-player-in-this-game") });
        try
        {
            return Ok(await new OrderEstimateService(_db).EstimateAsync(gameId, req, lng, nationId));
        }
        catch (OrderRequestException ex)
        {
            return StatusCode(ex.Status, new { error = ex.Message });
        }
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

