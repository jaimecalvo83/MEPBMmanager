using System.Security.Claims;
using System.Text.Json;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
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
    public async Task<IActionResult> List(string gameId)
    {
        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return Ok(new { orders = Array.Empty<object>(), turn = (object?)null });

        var orders = await _db.Orders
            .Include(o => o.Character)
            .Where(o => o.GameId == gameId && o.TurnId == currentTurn.Id && o.NationId == nationId)
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
        var nationId = await GetPlayerNationId(gameId);
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
        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.GameId == gameId && o.NationId == nationId && o.Status == "pending");

        if (order == null)
            return NotFound(new { error = "Order not found or cannot be cancelled" });

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Order cancelled" });
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> Validate(string gameId)
    {
        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var currentTurn = await _db.Turns
            .Where(t => t.GameId == gameId && t.Status == "orders_open")
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        if (currentTurn == null)
            return BadRequest(new { error = "No turn accepting orders" });

        var orders = await _db.Orders
            .Include(o => o.Character)
            .Where(o => o.GameId == gameId && o.TurnId == currentTurn.Id && o.NationId == nationId && o.Status == "pending")
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

    private async Task<string?> GetPlayerNationId(string gameId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId);
        return player?.NationId;
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
