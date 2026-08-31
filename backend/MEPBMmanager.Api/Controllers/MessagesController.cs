using System.Security.Claims;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Controllers;

[ApiController]
[Route("api/games/{gameId}/messages")]
public class MessagesController : ControllerBase
{
    private readonly MepbmDbContext _db;

    public MessagesController(MepbmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(string gameId, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var skip = (page - 1) * limit;

        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.GameId == gameId && m.SenderId == nationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.Subject,
                m.Content,
                m.IsRead,
                m.CreatedAt,
                m.SenderId,
                sender = new { m.Sender.Name, m.Sender.Color }
            })
            .ToListAsync();

        var total = await _db.Messages.CountAsync(m => m.GameId == gameId && m.SenderId == nationId);

        return Ok(new { messages, total, page });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Send(string gameId, [FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Subject and content are required" });

        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            GameId = gameId,
            SenderId = nationId,
            Subject = request.Subject,
            Content = request.Content
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            message = new
            {
                message.Id,
                message.GameId,
                message.SenderId,
                message.Subject,
                message.Content,
                message.IsRead,
                message.CreatedAt
            }
        });
    }

    [HttpPut("{messageId}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(string gameId, string messageId)
    {
        var nationId = await GetPlayerNationId(gameId);
        if (nationId == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null)
            return NotFound(new { error = "Message not found" });

        message.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Marked as read" });
    }

    private async Task<string?> GetPlayerNationId(string gameId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var player = await _db.Players
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId);
        return player?.NationId;
    }
}

public record SendMessageRequest(string Subject, string Content);
