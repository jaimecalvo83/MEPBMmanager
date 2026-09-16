using System.Security.Claims;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Domain.Constants;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly MepbmDbContext _db;

    public GamesController(MepbmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";

        IQueryable<Game> query = _db.Games;

        if (userRole == "test_admin")
        {
            // test_admin sees everything
        }
        else
        {
            // game_user: only games where they're a player or game admin
            query = query.Where(g => g.Players.Any(p => p.UserId == userId)
                                     || g.GameAdmins.Any(ga => ga.UserId == userId));
        }

        var games = await query
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Status,
                g.CurrentTurn,
                g.MaxTurns,
                g.TurnIntervalDays,
                g.CreatedAt,
                g.StartedAt,
                playerCount = g.Players.Count,
                nationCount = g.Nations.Count,
                isPlayer = g.Players.Any(p => p.UserId == userId),
                isGameAdmin = g.GameAdmins.Any(ga => ga.UserId == userId),
                gameTypeCode = g.GameType.Code
            })
            .ToListAsync();

        return Ok(new { games });
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";

        var game = await _db.Games
            .Include(g => g.GameAdmins)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        // Only game admin can delete (creator is always a game admin)
        var isGameAdmin = game.GameAdmins.Any(ga => ga.UserId == userId);

        if (!isGameAdmin && userRole != "test_admin")
            return BadRequest(new { error = "Only game admin can delete" });

        // Only allow deletion if game is in setup phase
        if (game.Status != "setup")
            return BadRequest(new { error = "Cannot delete game that has started" });

        // Delete all related entities first (foreign key constraints)
        var nationIds = await _db.Nations.Where(n => n.GameId == id).Select(n => n.Id).ToListAsync();

        // Delete child entities of Nations
        foreach (var nationId in nationIds)
        {
            _db.Characters.RemoveRange(await _db.Characters.Where(c => c.NationId == nationId).ToListAsync());
            _db.Armies.RemoveRange(await _db.Armies.Where(a => a.NationId == nationId).ToListAsync());
            _db.Navies.RemoveRange(await _db.Navies.Where(n => n.NationId == nationId).ToListAsync());
            _db.PopulationCentres.RemoveRange(await _db.PopulationCentres.Where(p => p.NationId == nationId).ToListAsync());
            _db.NationRelations.RemoveRange(await _db.NationRelations.Where(nr => nr.NationId == nationId).ToListAsync());
        }
        _db.Nations.RemoveRange(await _db.Nations.Where(n => n.GameId == id).ToListAsync());

        // Delete game-level entities
        _db.Players.RemoveRange(await _db.Players.Where(p => p.GameId == id).ToListAsync());
        _db.GameAdmins.RemoveRange(await _db.GameAdmins.Where(ga => ga.GameId == id).ToListAsync());
        _db.Turns.RemoveRange(await _db.Turns.Where(t => t.GameId == id).ToListAsync());
        _db.HexTiles.RemoveRange(await _db.HexTiles.Where(h => h.GameId == id).ToListAsync());
        _db.Messages.RemoveRange(await _db.Messages.Where(m => m.GameId == id).ToListAsync());
        _db.Orders.RemoveRange(await _db.Orders.Where(o => o.GameId == id).ToListAsync());
        _db.Encounters.RemoveRange(await _db.Encounters.Where(e => e.GameId == id).ToListAsync());
        _db.GameEvents.RemoveRange(await _db.GameEvents.Where(ge => ge.GameId == id).ToListAsync());
        _db.MarketPrices.RemoveRange(await _db.MarketPrices.Where(mp => mp.GameId == id).ToListAsync());

        // Finally delete the game
        _db.Games.Remove(game);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Game deleted successfully" });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateGameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Game name is required" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var code = string.IsNullOrWhiteSpace(request.GameTypeCode) ? "2950" : request.GameTypeCode;

        var gameType = await _db.GameTypes.FirstOrDefaultAsync(g => g.Code == code)
                       ?? await _db.GameTypes.FirstOrDefaultAsync(g => g.Code == "2950");
        if (gameType == null)
            return BadRequest(new { error = "Unknown game type" });

        var playerEmails = request.PlayerEmails ?? new List<string>();
        var adminEmails = (request.AdminEmails ?? new List<string>()).Select(e => e.Trim().ToLower()).ToList();
        var invitedUsers = new List<User>();

        foreach (var email in playerEmails)
        {
            var normalizedEmail = email.Trim().ToLower();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (user != null)
            {
                if (user.Id == userId)
                    return BadRequest(new { error = "Cannot invite yourself as player" });
                invitedUsers.Add(user);
            }
        }

        foreach (var adminEmail in adminEmails)
        {
            if (!playerEmails.Select(e => e.Trim().ToLower()).Contains(adminEmail))
                return BadRequest(new { error = $"Admin email {adminEmail} must be in the player list" });
        }

        if (adminEmails.Count < 1)
            return BadRequest(new { error = "Must invite at least 1 admin." });

        var game = new Game
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            GameTypeId = gameType.Id,
            MaxTurns = request.MaxTurns ?? 200,
            TurnIntervalDays = request.TurnIntervalDays ?? 14
        };
        _db.Games.Add(game);

        // Creator is always admin (accepted)
        _db.GameAdmins.Add(new GameAdmin
        {
            Id = Guid.NewGuid().ToString(),
            GameId = game.Id,
            UserId = userId,
            IsReady = true,
            AcceptedAt = DateTime.UtcNow
        });

        // Create players for existing users
        var creatorId = userId;
        foreach (var invitedUser in invitedUsers.Where(u => u.Id != creatorId).DistinctBy(u => u.Id))
        {
            _db.Players.Add(new Player
            {
                Id = Guid.NewGuid().ToString(),
                UserId = invitedUser.Id,
                GameId = game.Id,
                IsReady = false
            });

            if (adminEmails.Contains(invitedUser.Email))
            {
                _db.GameAdmins.Add(new GameAdmin
                {
                    Id = Guid.NewGuid().ToString(),
                    GameId = game.Id,
                    UserId = invitedUser.Id,
                    IsReady = false
                });
            }
        }

        // Probable players (email not in DB)
        foreach (var newEmail in playerEmails.Select(e => e.Trim().ToLower()).Distinct())
        {
            var userExists = await _db.Users.AnyAsync(u => u.Email == newEmail);
            if (userExists) continue;

            var alreadyInvited = invitedUsers.Any(u => u.Email == newEmail);
            if (alreadyInvited) continue;

            var tempUserId = Guid.NewGuid().ToString();
            _db.Players.Add(new Player
            {
                Id = Guid.NewGuid().ToString(),
                UserId = tempUserId,
                GameId = game.Id,
                Email = newEmail,
                IsReady = false
            });
        }

        await _db.SaveChangesAsync();

        return StatusCode(201, new { game = new { game.Id, game.Name, gameTypeCode = gameType.Code, game.Status } });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Get(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";

        var game = await _db.Games
            .Include(g => g.Nations)
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Include(g => g.Turns)
            .Include(g => g.HexTiles)
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return NotFound(new { error = "Game not found" });

        // test_admin sees everything; others must be a player
        if (userRole != "test_admin")
        {
            var isPlayer = game.Players.Any(p => p.UserId == userId);
            if (!isPlayer)
                return Forbid();
        }

        var result = new
        {
            game.Id,
            game.Name,
            gameTypeCode = game.GameType?.Code,
            game.Status,
            game.CurrentTurn,
            game.MaxTurns,
            game.TurnIntervalDays,
            game.CreatedAt,
            game.StartedAt,
            nations = game.Nations.Select(n => new
            {
                n.Id,
                n.Name,
                n.Allegiance,
                n.Color,
                n.TaxRate,
                n.Gold,
                n.Food,
                n.Timber,
                n.Leather,
                n.Bronze,
                n.Steel,
                n.Mithril,
                n.Mounts
            }),
            players = game.Players.Select(p => new { p.Id, p.NationId, p.Role, p.IsReady, user = new { p.User.Id, p.User.Username } }),
            turns = game.Turns.OrderByDescending(t => t.Number).Take(5)
                .Select(t => new { t.Id, t.Number, t.Status, t.Season, t.Deadline }),
            hexTiles = game.HexTiles
                .Select(h => new { h.Id, h.Q, h.R, h.Terrain, h.OwnerId, h.HasBridge, h.HasFord, h.HasMajorRiver, h.HasMinorRiver, h.HasRoad })
        };

        return Ok(new { game = result });
    }

    [HttpGet("{id}/nations")]
    [Authorize]
    public async Task<IActionResult> GetNations(string id)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        var takenNationIds = await _db.Players
            .Where(p => p.GameId == id && p.NationId != null)
            .Select(p => p.NationId!)
            .ToHashSetAsync();

        var nations = await _db.Nations
            .Where(n => n.GameId == id)
            .OrderBy(n => n.Name)
            .Select(n => new
            {
                n.Id,
                n.Name,
                n.Allegiance,
                n.Color,
                taken = takenNationIds.Contains(n.Id)
            })
            .ToListAsync();

        return Ok(new { nations });
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> Join(string id, [FromBody] JoinGameRequest request)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup" && game.Status != "active")
            return BadRequest(new { error = "Cannot join this game" });

        if (game.Status == "active" && request.NationId != null)
            return BadRequest(new { error = "Pick your nation after joining" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var existingPlayer = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);
        if (existingPlayer != null)
            return Conflict(new { error = "Already joined this game" });

        if (request.NationId != null)
        {
            var nation = await _db.Nations.FirstOrDefaultAsync(n => n.Id == request.NationId && n.GameId == id);
            if (nation == null)
                return NotFound(new { error = "Nation not found" });

            var nationTaken = await _db.Players.FirstOrDefaultAsync(p => p.NationId == request.NationId && p.GameId == id);
            if (nationTaken != null)
                return Conflict(new { error = "Nation already taken" });
        }

        var player = new Player
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            GameId = id,
            NationId = game.Status == "setup" ? request.NationId : null
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        return StatusCode(201, new { player = new { player.Id, player.UserId, player.GameId, player.NationId, player.Role, player.IsReady, player.JoinedAt } });
    }

    [HttpPut("{id}/nation")]
    [Authorize]
    public async Task<IActionResult> UpdateNation(string id, [FromBody] UpdateNationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);
        if (player == null)
            return NotFound(new { error = "You are not a player in this game" });

        // Setup: asignar libremente. Activa: solo reclamar si aún no tienes nación.
        if (game.Status != "setup" && !(game.Status == "active" && player.NationId == null))
            return BadRequest(new { error = "Can only claim a nation during setup, or an unassigned nation in an active game" });

        if (player.NationId != null)
            return BadRequest(new { error = "You already have a nation assigned" });

        var nation = await _db.Nations.FirstOrDefaultAsync(n => n.Id == request.NationId && n.GameId == id);
        if (nation == null)
            return NotFound(new { error = "Nation not found" });

        var nationTaken = await _db.Players.FirstOrDefaultAsync(p => p.NationId == request.NationId && p.GameId == id);
        if (nationTaken != null)
            return Conflict(new { error = "Nation already taken" });

        player.NationId = request.NationId;
        await _db.SaveChangesAsync();

        return Ok(new { player = new { player.Id, player.UserId, player.GameId, player.NationId, player.Role, player.IsReady, player.JoinedAt } });
    }

    [HttpPut("{id}/relations")]
    [Authorize]
    public async Task<IActionResult> SetRelation(string id, [FromBody] SetRelationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";

        var game = await _db.Games
            .Include(g => g.Players)
            .Include(g => g.GameAdmins)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        var player = game.Players.FirstOrDefault(p => p.UserId == userId);
        bool allowed = userRole == "test_admin"
            || game.GameAdmins.Any(ga => ga.UserId == userId)
            || (player?.NationId == request.NationId);
        if (!allowed)
            return Forbid();

        if (request.NationId == request.TargetNationId)
            return BadRequest(new { error = "Cannot set a relation with itself" });

        var nation = await _db.Nations.FirstOrDefaultAsync(n => n.Id == request.NationId && n.GameId == id);
        var target = await _db.Nations.FirstOrDefaultAsync(n => n.Id == request.TargetNationId && n.GameId == id);
        if (nation == null || target == null)
            return NotFound(new { error = "Nation not found" });

        var rel = await _db.NationRelations
            .FirstOrDefaultAsync(r => r.NationId == request.NationId && r.TargetNationId == request.TargetNationId);
        if (rel == null)
        {
            rel = new NationRelation
            {
                Id = Guid.NewGuid().ToString(),
                NationId = request.NationId,
                TargetNationId = request.TargetNationId
            };
            _db.NationRelations.Add(rel);
        }
        rel.Level = Math.Clamp(request.Level, -2, 2);
        await _db.SaveChangesAsync();

        return Ok(new { relation = new { rel.Id, rel.NationId, rel.TargetNationId, rel.Level } });
    }

    private void SeedInitialRelations(string gameId, List<Nation> nations)
    {
        // Misma lealtad => tolerados (nivel > 0 = paso permitido); resto neutral.
        foreach (var a in nations)
        {
            foreach (var b in nations)
            {
                if (a.Id == b.Id) continue;
                _db.NationRelations.Add(new NationRelation
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = a.Id,
                    TargetNationId = b.Id,
                    Level = a.Allegiance == b.Allegiance ? 1 : 0
                });
            }
        }
    }

    [HttpPost("{id}/leave")]
    [Authorize]
    public async Task<IActionResult> Leave(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Cannot leave a game that has started" });

        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);
        if (player == null)
            return NotFound(new { error = "You are not in this game" });

        _db.Players.Remove(player);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Left game" });
    }

    [HttpPost("{id}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(string id, [FromBody] AcceptGameRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Game is not in setup phase" });

        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);
        if (player == null)
            return NotFound(new { error = "You are not invited to this game" });

        if (player.IsReady)
            return BadRequest(new { error = "You have already accepted this game" });

        // Validate WantsToPlayWithUserId if provided
        if (!string.IsNullOrEmpty(request.WantsToPlayWithUserId))
        {
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.WantsToPlayWithUserId);
            if (targetUser == null)
                return BadRequest(new { error = "User not found" });

            // Must be a player in the same game
            var targetPlayer = await _db.Players.FirstOrDefaultAsync(p => p.UserId == request.WantsToPlayWithUserId && p.GameId == id);
            if (targetPlayer == null)
                return BadRequest(new { error = "Selected user is not in this game" });

            // Cannot choose yourself
            if (request.WantsToPlayWithUserId == userId)
                return BadRequest(new { error = "Cannot choose yourself" });
        }

        player.IsReady = true;
        player.AcceptedAt = DateTime.UtcNow;
        player.WantsToPlayWithUserId = request.WantsToPlayWithUserId;

        // Also update GameAdmin if user is a game admin
        var gameAdmin = await _db.GameAdmins.FirstOrDefaultAsync(ga => ga.UserId == userId && ga.GameId == id);
        if (gameAdmin != null && !gameAdmin.IsReady)
        {
            gameAdmin.IsReady = true;
            gameAdmin.AcceptedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Game accepted" });
    }

    [HttpGet("{id}/players")]
    [Authorize]
    public async Task<IActionResult> GetPlayers(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        // Check if user is a player in this game (test_admin bypasses this)
        if (userRole != "test_admin")
        {
            var currentPlayer = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);
            if (currentPlayer == null)
                return Forbid();
        }

        var isAdmin = await _db.GameAdmins.AnyAsync(ga => ga.GameId == id && ga.UserId == userId);

        var players = await _db.Players
            .Include(p => p.User)
            .Include(p => p.WantsToPlayWith)
            .Where(p => p.GameId == id)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync();

        var result = players.Select(p => new
        {
            p.Id,
            p.UserId,
            username = p.User?.Username,
            email = p.User?.Email ?? p.Email,
            p.Role,
            p.IsReady,
            p.AcceptedAt,
            p.JoinedAt,
            // Only admins can see who others want to play with
            wantsToPlayWith = isAdmin ? (p.WantsToPlayWith == null ? null : new
            {
                userId = p.WantsToPlayWith.Id,
                username = p.WantsToPlayWith.Username
            }) : null
        });

        return Ok(new { players = result });
    }

    [HttpGet("{id}/admins")]
    [Authorize]
    public async Task<IActionResult> GetAdmins(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        // Check if user is a game admin
        var isAdmin = await _db.GameAdmins.AnyAsync(ga => ga.GameId == id && ga.UserId == userId);
        if (!isAdmin)
            return Forbid();

        var admins = await _db.GameAdmins
            .Include(ga => ga.User)
            .Where(ga => ga.GameId == id)
            .OrderBy(ga => ga.CreatedAt)
            .ToListAsync();

        var result = admins.Select(ga => new
        {
            ga.Id,
            ga.UserId,
            username = ga.User?.Username,
            email = ga.User?.Email,
            ga.IsReady,
            ga.AcceptedAt,
            ga.CreatedAt
        });

        return Ok(new { admins = result });
    }

    [HttpPost("{id}/players")]
    [Authorize]
    public async Task<IActionResult> AddPlayer(string id, [FromBody] AddPlayerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games
            .Include(g => g.GameAdmins)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Cannot add players after game has started" });

        var isAdmin = game.GameAdmins.Any(ga => ga.UserId == userId);
        if (!isAdmin)
            return Forbid();

        var email = request.Email.Trim().ToLower();

        // Check if already a player
        var existingPlayer = game.Players.FirstOrDefault(p =>
            (p.User != null && p.User.Email == email) ||
            (p.Email == email));
        if (existingPlayer != null)
            return BadRequest(new { error = "User is already a player in this game" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            // Existing user — create Player linked to User
            _db.Players.Add(new Player
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                GameId = id,
                IsReady = false
            });

            // If admin checkbox, also create GameAdmin
            if (request.IsAdmin)
            {
                var alreadyAdmin = game.GameAdmins.Any(ga => ga.UserId == user.Id);
                if (!alreadyAdmin)
                {
                    _db.GameAdmins.Add(new GameAdmin
                    {
                        Id = Guid.NewGuid().ToString(),
                        GameId = id,
                        UserId = user.Id,
                        IsReady = false
                    });
                }
            }
        }
        else
        {
            // Probable player — no user in DB yet
            var tempUserId = Guid.NewGuid().ToString();
            _db.Players.Add(new Player
            {
                Id = Guid.NewGuid().ToString(),
                UserId = tempUserId,
                GameId = id,
                Email = email,
                IsReady = false
            });

            if (request.IsAdmin)
            {
                // Cannot create GameAdmin for probable players (no User in DB → FK violation)
                // Admin role will be assigned when the user registers and is added to the game
            }
        }

        await _db.SaveChangesAsync();

        // TODO: Send invitation email
        return Ok(new { message = "Player added", email = email });
    }

    [HttpDelete("{id}/players/{playerId}")]
    [Authorize]
    public async Task<IActionResult> RemovePlayer(string id, string playerId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games
            .Include(g => g.GameAdmins)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Cannot remove players after game has started" });

        var isAdmin = game.GameAdmins.Any(ga => ga.UserId == userId);
        if (!isAdmin)
            return Forbid();

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId && p.GameId == id);
        if (player == null)
            return NotFound(new { error = "Player not found" });

        if (player.IsReady)
            return BadRequest(new { error = "Cannot remove a player who has already confirmed" });

        // Also remove GameAdmin record if exists
        var gameAdmin = await _db.GameAdmins.FirstOrDefaultAsync(ga => ga.UserId == player.UserId && ga.GameId == id);
        if (gameAdmin != null)
            _db.GameAdmins.Remove(gameAdmin);

        _db.Players.Remove(player);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Player removed" });
    }

    [HttpPost("{id}/accept-admin")]
    [Authorize]
    public async Task<IActionResult> AcceptAdmin(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Game is not in setup phase" });

        var gameAdmin = await _db.GameAdmins.FirstOrDefaultAsync(ga => ga.GameId == id && ga.UserId == userId);
        if (gameAdmin == null)
            return NotFound(new { error = "You are not invited as admin to this game" });

        if (gameAdmin.IsReady)
            return BadRequest(new { error = "You have already accepted this admin role" });

        gameAdmin.IsReady = true;
        gameAdmin.AcceptedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Admin role accepted" });
    }

    [HttpPost("{id}/start")]
    [Authorize]
    public async Task<IActionResult> Start(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Include(g => g.Players).ThenInclude(p => p.WantsToPlayWith)
            .Include(g => g.GameAdmins).ThenInclude(ga => ga.User)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Game has already started" });

        var isAdmin = game.GameAdmins.Any(ga => ga.UserId == userId);
        if (!isAdmin)
            return Forbid();

        var confirmedPlayers = game.Players.Where(p => p.IsReady).ToList();

        var pendingAdmins = game.GameAdmins.Where(ga => !ga.IsReady).ToList();
        if (pendingAdmins.Any())
        {
            var pendingEmails = pendingAdmins.Select(pa => pa.User?.Email).ToList();
            return BadRequest(new { error = "Not all admins have accepted yet", pendingAdmins = pendingEmails });
        }

        // ── Real 2950 module (digitized map + full module setup) ──
        var gameTypeCode = (await _db.GameTypes.AsNoTracking().FirstOrDefaultAsync(g => g.Id == game.GameTypeId))?.Code ?? "";
        if (gameTypeCode == "2950")
        {
            var c = confirmedPlayers.Count;
            if (!((c >= 6 && c <= 10) || (c >= 11 && c <= 15) || (c >= 16 && c <= 20) || (c >= 21 && c <= 25)))
                return BadRequest(new { error = "2950 needs 6-10, 11-15, 16-20 or 21-25 confirmed players (templates 4+4+2 / 6+6+3 / 8+8+4 / 10+10+5, surplus nations as NPCs)" });
            var nationCount = await StartReal2950Game(game);
            return Ok(new { message = "Game started (2950 real module)", nations = nationCount, players = confirmedPlayers.Count });
        }

        if (confirmedPlayers.Count < 1)
            return BadRequest(new { error = "Need at least 1 confirmed player to start" });

        // ── Get scenario templates ──
        var templates = await _db.NationTemplates.Where(t => t.GameTypeId == game.GameTypeId).ToListAsync();
        var freeTemplates = templates.Where(t => t.Allegiance == "free_peoples").ToList();
        var darkTemplates = templates.Where(t => t.Allegiance == "dark_servants").ToList();
        var neutralTemplates = templates.Where(t => t.Allegiance == "neutral").ToList();

        // ── Determine how many nations per allegiance ──
        // Equal split between main alliances; neutrals only as flavor
        int playerCount = confirmedPlayers.Count;
        int freeCount = playerCount / 2;
        int darkCount = playerCount / 2;

        // If odd number of players, give the extra to the larger available side
        if (freeCount + darkCount < playerCount)
        {
            if (freeCount < darkCount)
                freeCount++;
            else
                darkCount++;
        }

        // Ensure we don't exceed available templates
        freeCount = Math.Min(freeCount, freeTemplates.Count);
        darkCount = Math.Min(darkCount, darkTemplates.Count);

        // Add neutral nations for flavor (1-2 depending on size)
        int neutralCount = Math.Min(playerCount >= 6 ? 2 : 1, neutralTemplates.Count);

        // Total nations on the map
        int totalNations = freeCount + darkCount + neutralCount;

        // ── Select specific templates ──
        var rng = new Random();
        var selectedFree = freeTemplates.OrderBy(_ => rng.Next()).Take(freeCount).ToList();
        var selectedDark = darkTemplates.OrderBy(_ => rng.Next()).Take(darkCount).ToList();
        var selectedNeutral = neutralTemplates.OrderBy(_ => rng.Next()).Take(neutralCount).ToList();
        var allSelected = selectedFree.Concat(selectedDark).Concat(selectedNeutral).ToList();

        // ── Calculate adaptive map size ──
        int mapRadius = CalculateMapRadius(totalNations);

        // ── Create hex map ──
        CreateAdaptiveMap(game.Id, game.GameTypeId!, mapRadius, _db);

        // ── Compute start hexes for each nation (spread around the map) ──
        int nationIndex = 0;
        int totalNationsCount = allSelected.Count;

        // ── Create nations from selected templates ──
        var createdNations = new List<Nation>();
        foreach (var t in allSelected)
        {
            // Compute a unique start hex spread around the map
            var startHex = ComputeNationStartHex(nationIndex++, totalNationsCount, mapRadius, t.Allegiance);

            var nation = new Nation
            {
                Id = Guid.NewGuid().ToString(),
                GameId = game.Id,
                Name = t.Name,
                Allegiance = t.Allegiance,
                Color = t.Color,
                StartHex = startHex,
                Gold = t.StartingGold,
                Food = t.StartingFood,
                Timber = t.StartingTimber,
                Leather = t.StartingLeather,
                Bronze = t.StartingBronze,
                Steel = t.StartingSteel,
                Mithril = t.StartingMithril,
                Mounts = t.StartingMounts,
                TaxRate = t.TaxRate
            };
            _db.Nations.Add(nation);
            createdNations.Add(nation);

            // Capital
            _db.PopulationCentres.Add(new PopulationCentre
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = t.CapitalName,
                Size = t.CapitalSize,
                LocationHex = t.StartHex,
                Loyalty = 70,
                Production = t.CapitalSize switch { "citadel" => 400, "city" => 300, "fortress" => 250, "town" => 200, _ => 100 },
                Stores = t.CapitalSize switch { "citadel" => 1000, "city" => 800, "fortress" => 600, "town" => 400, _ => 200 },
                IsCapital = true,
                HasHarbour = t.CapitalHasHarbour,
                HasPort = t.CapitalHasPort,
                Fortification = t.CapitalFortification
            });

            // Border town (adjacent hex to capital)
            var borderHex = GetAdjacentHex(t.StartHex, rng);
            _db.PopulationCentres.Add(new PopulationCentre
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = t.BorderTownName,
                Size = t.BorderTownSize,
                LocationHex = borderHex,
                Loyalty = 55,
                Production = 100,
                Stores = 200,
                IsCapital = false,
                HasHarbour = false,
                HasPort = false,
                Fortification = t.BorderTownFortification
            });

            // Main Army (at capital)
            _db.Armies.Add(new Army
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = "Main Army",
                LocationHex = t.StartHex,
                HeavyCavalry = t.StartingHeavyCavalry,
                LightCavalry = t.StartingLightCavalry,
                HeavyInfantry = t.StartingHeavyInfantry,
                LightInfantry = t.StartingLightInfantry,
                Archers = t.StartingArchers,
                MenAtArms = t.StartingMenAtArms,
                HCWeaponRank = t.HCWeaponRank,
                HCArmourRank = t.HCArmourRank,
                LCWeaponRank = t.LCWeaponRank,
                LCArmourRank = t.LCArmourRank,
                HIWeaponRank = t.HIWeaponRank,
                HIArmourRank = t.HIArmourRank,
                LIWeaponRank = t.LIWeaponRank,
                LIArmourRank = t.LIArmourRank,
                ArcherWeaponRank = t.ArcherWeaponRank,
                ArcherArmourRank = t.ArcherArmourRank,
                MAAWeaponRank = t.MAAWeaponRank,
                MAAArmourRank = t.MAAArmourRank,
                Morale = t.StartingMorale,
                Training = t.StartingTraining,
                Food = 2000,
                WarMachines = 0,
                IsOnManoeuvres = false
            });
        }

        // ── Smart nation assignment ──
        var freeNations = createdNations.Where(n => n.Allegiance == "free_peoples").ToList();
        var darkNations = createdNations.Where(n => n.Allegiance == "dark_servants").ToList();
        var playerNations = new Dictionary<string, string>(); // playerId -> nationId

        // First pass: pair players who want to play together (same allegiance)
        var assignedPlayers = new HashSet<string>();
        var assignedNations = new HashSet<string>();

        // Build pairs from wantsToPlayWith
        var pairs = new List<(string playerId, string partnerId)>();
        foreach (var p in confirmedPlayers)
        {
            if (p.WantsToPlayWithUserId != null && !assignedPlayers.Contains(p.Id))
            {
                var partner = confirmedPlayers.FirstOrDefault(q => q.UserId == p.WantsToPlayWithUserId && q.Id != p.Id);
                if (partner != null && !assignedPlayers.Contains(partner.Id))
                {
                    pairs.Add((p.Id, partner.Id));
                    assignedPlayers.Add(p.Id);
                    assignedPlayers.Add(partner.Id);
                }
            }
        }

        // Assign pairs to same allegiance if possible
        foreach (var (playerId, partnerId) in pairs)
        {
            // Try free peoples first
            var freePair = freeNations.Where(n => !assignedNations.Contains(n.Id)).Take(2).ToList();
            if (freePair.Count >= 2)
            {
                playerNations[playerId] = freePair[0].Id;
                playerNations[partnerId] = freePair[1].Id;
                assignedNations.Add(freePair[0].Id);
                assignedNations.Add(freePair[1].Id);
                continue;
            }

            // Try dark servants
            var darkPair = darkNations.Where(n => !assignedNations.Contains(n.Id)).Take(2).ToList();
            if (darkPair.Count >= 2)
            {
                playerNations[playerId] = darkPair[0].Id;
                playerNations[partnerId] = darkPair[1].Id;
                assignedNations.Add(darkPair[0].Id);
                assignedNations.Add(darkPair[1].Id);
                continue;
            }

            // Can't pair them together, assign individually below
            assignedPlayers.Remove(playerId);
            assignedPlayers.Remove(partnerId);
        }

        // Second pass: assign remaining unpaired players
        var remainingPlayers = confirmedPlayers.Where(p => !assignedPlayers.Contains(p.Id)).ToList();
        var availableNations = createdNations.Where(n => !assignedNations.Contains(n.Id))
            .OrderBy(_ => rng.Next()).ToList();

        foreach (var player in remainingPlayers)
        {
            var nation = availableNations.FirstOrDefault(n => !assignedNations.Contains(n.Id));
            if (nation != null)
            {
                playerNations[player.Id] = nation.Id;
                assignedNations.Add(nation.Id);
            }
        }

        // Assign nations to players
        foreach (var player in game.Players)
        {
            if (playerNations.TryGetValue(player.Id, out var nationId))
            {
                player.NationId = nationId;
            }
        }

        // ── Initial relations: same allegiance tolerated, rest neutral ──
        SeedInitialRelations(game.Id, createdNations);

        // ── Initialize characters for each player nation ──
        await _db.SaveChangesAsync(); // Save nations/armies/PCs first so InitializeNationAtStart can find them
        foreach (var playerId in playerNations.Keys)
        {
            InitializeNationAtStart(game.Id, playerNations[playerId], rng);
        }

        // ── Create first turn ──
        var deadline = DateTime.UtcNow.AddDays(game.TurnIntervalDays);
        _db.Turns.Add(new Turn
        {
            Id = Guid.NewGuid().ToString(),
            GameId = id,
            Number = 1,
            Status = "orders_open",
            Season = GetCurrentSeason(),
            Deadline = deadline
        });

        game.Status = "active";
        game.CurrentTurn = 1;
        game.StartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Game started", nations = createdNations.Count, players = playerNations.Count });
    }

    [HttpPost("{id}/process-turn")]
    [Authorize]
    public async Task<IActionResult> ProcessTurn(string id, [FromServices] TurnProcessor processor)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games
            .Include(g => g.GameAdmins)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "active")
            return BadRequest(new { error = "Game is not active" });

        var isAdmin = game.GameAdmins.Any(ga => ga.UserId == userId);
        if (!isAdmin)
            return Forbid();

        var result = await processor.ProcessTurnAsync(id);
        if (result == null)
            return NotFound(new { error = "Game not found" });

        return Ok(result);
    }

    [HttpGet("{id}/turns/{turnId}/report")]
    [Authorize]
    public async Task<IActionResult> GetTurnReport(string id, string turnId, [FromServices] TurnReportService reportService)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        var player = game.Players.FirstOrDefault(p => p.UserId == userId);
        var nationId = player?.NationId;

        var report = await reportService.GetReportAsync(id, turnId, nationId);
        if (report == null)
            return NotFound(new { error = "Turn not found" });

        return Ok(report);
    }

    private void InitializeNationAtStart(string gameId, string nationId, Random rng)
    {
        var nation = _db.Nations.FirstOrDefault(n => n.Id == nationId);
        if (nation == null) return;
        var capitalHex = nation.StartHex ?? "0,0";

        // Buscar la plantilla correspondiente para obtener datos enriquecidos
        var game = _db.Games.FirstOrDefault(g => g.Id == gameId);
        var template = game?.GameTypeId != null
            ? _db.NationTemplates.FirstOrDefault(t => t.GameTypeId == game.GameTypeId && t.Name == nation.Name)
            : null;

        // ── Personajes ──
        var ruler = CreateCharacter(nationId, template?.Character1Name ?? "Ruler", "commander", command: 13, agent: 10, emissary: 10, mage: 10, captain: true, hex: capitalHex, champion: true);
        _db.Characters.Add(ruler);

        var commander2 = CreateCharacter(nationId, template?.Character2Name ?? "Commander", "commander", command: 11, agent: 8, emissary: 8, mage: 8, captain: false, hex: capitalHex);
        _db.Characters.Add(commander2);

        var commander3 = CreateCharacter(nationId, template?.Character3Name ?? "Marshal", "commander", command: 10, agent: 7, emissary: 7, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(commander3);

        var agent = CreateCharacter(nationId, template?.Character4Name ?? "Spymaster", "agent", command: 7, agent: 12, emissary: 9, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(agent);

        var emissary = CreateCharacter(nationId, template?.Character5Name ?? "Emissary", "emissary", command: 7, agent: 8, emissary: 12, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(emissary);

        var mage = CreateCharacter(nationId, template?.Character6Name ?? "Sage", "mage", command: 7, agent: 7, emissary: 8, mage: 12, captain: false, hex: capitalHex);
        _db.Characters.Add(mage);

        // ── Asignar ruler al ejército existente ──
        var army = _db.Armies.FirstOrDefault(a => a.NationId == nationId && a.Name == "Main Army");
        if (army != null)
        {
            army.CommanderId = ruler.Id;
            ruler.ArmyId = army.Id;
        }
    }

    private static Character CreateCharacter(
        string nationId, string name, string type,
        int command, int agent, int emissary, int mage,
        bool captain, string hex, bool champion = false)
    {
        return new Character
        {
            Id = Guid.NewGuid().ToString(),
            NationId = nationId,
            Name = name,
            Type = type,
            IsChampion = champion,
            CommandSkill = command,
            AgentSkill = agent,
            EmissarySkill = emissary,
            MageSkill = mage,
            Health = 100,
            MaxHealth = 100,
            Stealth = 0,
            ChallengeRank = 0,
            LocationHex = hex,
            IsDead = false,
            IsKidnapped = false,
            HeldByNationId = null,
            CompanyId = null,
            ArmyId = null
        };
    }

    [HttpGet("{id}/state")]
    [Authorize]
    public async Task<IActionResult> State(string id, [FromQuery] string? nationId = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "game_user";

        var player = await _db.Players
            .Include(p => p.Nation)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);

        var isTestAdmin = userRole == "test_admin";
        var isGameAdmin = await _db.GameAdmins.AnyAsync(ga => ga.GameId == id && ga.UserId == userId);

        if (player == null && !isTestAdmin && !isGameAdmin)
            return StatusCode(403, new { error = "Not a player in this game" });

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        var currentTurn = await _db.Turns
            .Where(t => t.GameId == id && t.Number == game!.CurrentTurn)
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();

        var recentTurns = await _db.Turns
            .Where(t => t.GameId == id)
            .OrderByDescending(t => t.Number)
            .Take(5)
            .Select(t => new { t.Id, t.Number, t.Status, t.Season, t.Deadline, t.ProcessedAt })
            .ToListAsync();

        // ── Determine switchable nations and active nation ──
        string? activeNationId = null;
        List<string> switchableNationIds; // nations the user can switch between

        if (isTestAdmin)
        {
            // test_admin: can switch between ALL nations
            switchableNationIds = await _db.Nations
                .Where(n => n.GameId == id)
                .OrderBy(n => n.Name)
                .Select(n => n.Id)
                .ToListAsync();

            if (nationId != null && switchableNationIds.Contains(nationId))
                activeNationId = nationId;
            else
                activeNationId = player?.NationId ?? switchableNationIds.FirstOrDefault();
        }
        else if (isGameAdmin && player?.NationId != null)
        {
            // game_admin: can switch between nations of SAME allegiance only
            var myAllegiance = player.Nation!.Allegiance;
            switchableNationIds = await _db.Nations
                .Where(n => n.GameId == id && n.Allegiance == myAllegiance)
                .OrderBy(n => n.Name)
                .Select(n => n.Id)
                .ToListAsync();

            if (nationId != null && switchableNationIds.Contains(nationId))
                activeNationId = nationId;
            else
                activeNationId = player.NationId;
        }
        else
        {
            // Regular player or game_admin without a nation: only their own
            switchableNationIds = player?.NationId != null
                ? new List<string> { player.NationId }
                : new List<string>();
            activeNationId = player?.NationId;
        }

        // ── Fetch entities ONLY for the active nation ──
        var activeSet = activeNationId != null
            ? new HashSet<string> { activeNationId }
            : new HashSet<string>();

        var characters = await _db.Characters.Where(c => activeSet.Contains(c.NationId))
            .Include(c => c.Artifacts).Include(c => c.Spells).ToListAsync();
        var armies = await _db.Armies.Where(a => activeSet.Contains(a.NationId)).ToListAsync();
        var navies = await _db.Navies.Where(n => activeSet.Contains(n.NationId)).ToListAsync();
        var populationCentres = await _db.PopulationCentres.Where(p => activeSet.Contains(p.NationId)).ToListAsync();
        var hexTiles = await _db.HexTiles.Where(h => h.GameId == id).ToListAsync();
        var relations = await _db.NationRelations.Where(nr => activeSet.Contains(nr.NationId)).ToListAsync();

        // ── Nation list: all switchable nations with basic info ──
        var visibleNations = await _db.Nations
            .Where(n => switchableNationIds.Contains(n.Id))
            .Select(n => new
            {
                n.Id,
                n.Name,
                n.Allegiance,
                n.Color,
                n.TaxRate,
                n.Gold,
                n.Food,
                n.Timber,
                n.Leather,
                n.Bronze,
                n.Steel,
                n.Mithril,
                n.Mounts,
                n.VictoryPoints,
                n.WarshipStrength
            })
            .ToListAsync();

        var activeNation = activeNationId != null
            ? visibleNations.FirstOrDefault(n => n.Id == activeNationId)
            : null;

        // ── Habilidades especiales del módulo (NationAbilities por nombre) ──
        var abilities = new List<object>();
        if (activeNation != null && NationAbilities.SlugByDisplayName.TryGetValue(activeNation.Name, out var slug)
            && NationAbilities.ByNationSlug.TryGetValue(slug, out var ids))
            abilities.AddRange(ids.Select(a => new { id = a, name = NationAbilities.DisplayNames.TryGetValue(a, out var n) ? n : a }));

        // ── Basic roster (all nations) for tabs like Relations ──
        var allNations = await _db.Nations
            .Where(n => n.GameId == id)
            .OrderBy(n => n.Name)
            .Select(n => new { n.Id, n.Name, n.Allegiance, n.Color, n.VictoryPoints, n.IsEliminated })
            .ToListAsync();

        return Ok(new
        {
            game = new { game!.Id, game.Name, gameTypeCode = game.GameType?.Code, game.Status, game.CurrentTurn, game.MaxTurns, game.TurnIntervalDays },
            player = player == null ? null : new { player.Id, player.UserId, player.GameId, player.NationId, player.IsReady, player.JoinedAt },
            nation = activeNation == null ? null : new
            {
                activeNation.Id,
                activeNation.Name,
                activeNation.Allegiance,
                activeNation.Color,
                activeNation.TaxRate,
                activeNation.Gold,
                activeNation.Food,
                activeNation.Timber,
                activeNation.Leather,
                activeNation.Bronze,
                activeNation.Steel,
                activeNation.Mithril,
                activeNation.Mounts,
                activeNation.VictoryPoints,
                activeNation.WarshipStrength,
                abilities
            },
            nations = visibleNations,
            allNations,
            currentTurn = currentTurn == null ? null : new { currentTurn.Id, currentTurn.Number, currentTurn.Status, currentTurn.Season, currentTurn.Deadline },
            turns = recentTurns,
            isGameAdmin,
            characters = characters.Select(c => new
            {
                c.Id,
                c.NationId,
                c.Name,
                c.Type,
                c.IsChampion,
                c.CommandSkill,
                c.AgentSkill,
                c.EmissarySkill,
                c.MageSkill,
                c.Health,
                c.MaxHealth,
                c.Stealth,
                c.ChallengeRank,
                c.LocationHex,
                c.IsDead,
                c.IsKidnapped,
                c.HeldByNationId,
                c.CompanyId,
                c.ArmyId,
                artifacts = c.Artifacts.Select(a => new { a.Id, a.Name, a.Type, a.Bonus, a.Alignment, a.LocationHex, a.NationId,
                    wikiId = ArtifactCatalog2950.Find(a.Name)?.Id,
                    primaryBenefit = ArtifactCatalog2950.Find(a.Name)?.Primary,
                    secondaryPower = ArtifactCatalog2950.Find(a.Name)?.Secondary }),
                spells = c.Spells.Where(s => s.IsKnown).Select(s => new
                {
                    s.SpellId,
                    name = SpellCatalog.Get(s.SpellId)?.Name ?? $"Spell {s.SpellId}",
                    s.Rank,
                    college = SpellCatalog.Get(s.SpellId)?.Type.ToString()
                })
            }),
            armies = armies.Select(a => new
            {
                a.Id,
                a.NationId,
                a.Name,
                a.LocationHex,
                a.CommanderId,
                a.HeavyCavalry,
                a.LightCavalry,
                a.HeavyInfantry,
                a.LightInfantry,
                a.Archers,
                a.MenAtArms,
                a.HCWeaponRank,
                a.HCArmourRank,
                a.LCWeaponRank,
                a.LCArmourRank,
                a.HIWeaponRank,
                a.HIArmourRank,
                a.LIWeaponRank,
                a.LIArmourRank,
                a.ArcherWeaponRank,
                a.ArcherArmourRank,
                a.MAAWeaponRank,
                a.MAAArmourRank,
                a.Morale,
                a.Training,
                a.Food,
                a.WarMachines,
                a.IsOnManoeuvres
            }),
            navies = navies.Select(n => new
            {
                n.Id,
                n.NationId,
                n.Warships,
                n.Transports,
                n.LocationHex,
                n.CommanderId,
                n.Strength
            }),
            populationCentres = populationCentres.Select(pc => new
            {
                pc.Id,
                pc.NationId,
                pc.Name,
                pc.Size,
                pc.LocationHex,
                pc.Loyalty,
                pc.Production,
                pc.Stores,
                pc.IsCapital,
                pc.IsHidden,
                pc.IsSieged,
                pc.HasHarbour,
                pc.HasPort,
                pc.Fortification
            }),
            hexTiles = hexTiles.Select(h => new { h.Id, q = h.Q, r = h.R, h.Terrain, h.OwnerId, h.HasBridge, h.HasFord, h.HasMajorRiver, h.HasMinorRiver, h.HasRoad }),
            relations = relations.Select(rel => new { rel.Id, rel.TargetNationId, rel.Level })
        });
    }

    private async Task<int> StartReal2950Game(Game game)
    {
        var data = Map2950Seeder.Load(AppContext.BaseDirectory);

        // ── Plantilla por baremo + PNJs (naciones sobrantes, máx 4) ──
        var rng = new Random();
        var confirmedCount = game.Players.Count(p => p.IsReady);
        var templateSize = Map2950Seeder.TemplateSizeFor(confirmedCount);
        var template = Map2950Seeder.Templates[templateSize];
        var (playedSlugs, _) = Map2950Seeder.SplitNpcs(template, confirmedCount);
        var selectedSlugs = new HashSet<string>(
            template.Free.Concat(template.Dark).Concat(template.Neutral));

        // ── Create the cropped 2950 hex map (recorte de plantilla ∪ contenido) ──
        var q1 = template.Q1; var q2 = template.Q2; var r1 = template.R1; var r2 = template.R2;
        foreach (var c in data.Centres.Where(c => selectedSlugs.Contains(c.NationSlug)))
        { q1 = Math.Min(q1, c.Q); q2 = Math.Max(q2, c.Q); r1 = Math.Min(r1, c.R); r2 = Math.Max(r2, c.R); }
        foreach (var a in data.Armies.Where(a => selectedSlugs.Contains(a.NationSlug)))
        { q1 = Math.Min(q1, a.Q); q2 = Math.Max(q2, a.Q); r1 = Math.Min(r1, a.R); r2 = Math.Max(r2, a.R); }
        foreach (var c in data.Characters.Where(c => selectedSlugs.Contains(c.NationSlug)))
        { q1 = Math.Min(q1, c.Q); q2 = Math.Max(q2, c.Q); r1 = Math.Min(r1, c.R); r2 = Math.Max(r2, c.R); }
        Map2950Seeder.CreateMap(game.Id, game.GameTypeId!, data, _db, (q1, q2, r1, r2));

        // ── Create the selected module nations (jugadas + PNJ) ──
        // Recursos iniciales: nationStats del JSON (Game 299 Turn 0) si existe, si no valores por defecto.
        var nationsBySlug = new Dictionary<string, Nation>();
        var allNations = new List<Nation>();
        var metasBySlug = data.Nations.ToDictionary(n => n.Slug);
        foreach (var slug in template.Free.Concat(template.Dark).Concat(template.Neutral))
        {
            var meta = metasBySlug[slug];
            data.NationStats.TryGetValue(meta.Slug, out var st);
            var nation = new Nation
            {
                Id = Guid.NewGuid().ToString(),
                GameId = game.Id,
                Name = meta.DisplayName,
                Allegiance = meta.Allegiance,
                Color = meta.Color,
                StartHex = meta.CapitalHex ?? "22,20",
                Gold = st?.Gold ?? 10000,
                Food = st?.Food ?? 5000,
                Timber = st?.Timber ?? 2000,
                Leather = st?.Leather ?? 1000,
                Bronze = st?.Bronze ?? 500,
                Steel = st?.Steel ?? 200,
                Mithril = st?.Mithril ?? 50,
                Mounts = st?.Mounts ?? 300,
                TaxRate = st?.TaxRate ?? 40,
                VictoryPoints = st?.VictoryPoints ?? 0,
                WarshipStrength = st?.WarshipStrength ?? 3
            };
            _db.Nations.Add(nation);
            nationsBySlug[meta.Slug] = nation;
            allNations.Add(nation);
        }

        // ── Create all population centres from the module ──
        foreach (var c in data.Centres)
        {
            if (!nationsBySlug.TryGetValue(c.NationSlug, out var nation))
                continue;

            var size = c.Size switch
            {
                "City" => "city",
                "Major Town" => "major town",
                "Town" => "town",
                "Village" => "village",
                "Camp" => "camp",
                _ => "village"
            };
            var fort = c.Fortification switch
            {
                "Citadel" => "Citadel Walls",
                "Castle" => "Stone Walls",
                "Stone Walls" => "Stone Walls",
                "Keep" => "Walls",
                "Fort" => "Fort",
                "Tower" => "Tower",
                _ => ""
            };
            var production = size switch { "citadel" => 400, "city" => 300, "fortress" => 250, "major town" => 250, "town" => 200, "camp" => 60, _ => 100 };
            var stores = size switch { "citadel" => 1000, "city" => 800, "fortress" => 600, "major town" => 600, "town" => 400, "camp" => 100, _ => 200 };
            // Lealtad Turn 0 (Game 299): capital 75, Major Town 75, Town 55, Village 40, Camp 30.
            var loyalty = c.IsCapital ? 75 : size switch
            {
                "major town" => 75,
                "town" => 55,
                "village" => 40,
                "camp" => 30,
                _ => 55
            };

            _db.PopulationCentres.Add(new PopulationCentre
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = c.Name,
                Size = size,
                LocationHex = $"{c.Q},{c.R}",
                Loyalty = loyalty,
                Production = production,
                Stores = stores,
                IsCapital = c.IsCapital,
                IsHidden = c.IsHidden,
                IsSieged = false,
                HasHarbour = c.HasHarbour,
                HasPort = c.HasPort,
                Fortification = fort
            });
        }

        // ── Create armies and navies from the module ──
        foreach (var a in data.Armies)
        {
            if (!nationsBySlug.TryGetValue(a.NationSlug, out var nation))
                continue;

            if (a.TotalLandTroops > 0)
            {
                // Training/armas/armadura/comida del JSON (Game 299 Turn 0) si existen, si no genéricos.
                _db.Armies.Add(new Army
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = nation.Id,
                    Name = $"{nation.Name} Army (Start)",
                    LocationHex = $"{a.Q},{a.R}",
                    IsOnManoeuvres = false,
                    HeavyCavalry = a.HeavyCavalry,
                    LightCavalry = a.LightCavalry,
                    HeavyInfantry = a.HeavyInfantry,
                    LightInfantry = a.LightInfantry,
                    Archers = a.Archers,
                    MenAtArms = a.MenAtArms,
                    HCWeaponRank = a.Weapons,
                    HCArmourRank = a.Armour,
                    LCWeaponRank = a.Weapons,
                    LCArmourRank = a.Armour,
                    HIWeaponRank = a.Weapons,
                    HIArmourRank = a.Armour,
                    LIWeaponRank = a.Weapons,
                    LIArmourRank = a.Armour,
                    ArcherWeaponRank = a.Weapons,
                    ArcherArmourRank = a.Armour,
                    MAAWeaponRank = a.Weapons,
                    MAAArmourRank = a.Armour,
                    Morale = a.Morale,
                    Training = a.Training,
                    WarMachines = 0,
                    Food = a.Food
                });
            }

            if (a.Warships > 0 || a.Transports > 0)
            {
                _db.Navies.Add(new Navy
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = nation.Id,
                    Warships = a.Warships,
                    Transports = a.Transports,
                    LocationHex = $"{a.Q},{a.R}",
                    Strength = nation.WarshipStrength
                });
            }
        }

        // ── Create starting characters from the module (wiki.mepbm.com/2950) ──
        foreach (var c in data.Characters)
        {
            if (!nationsBySlug.TryGetValue(c.NationSlug, out var nation))
                continue;

            var ch = new Character
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = c.Name,
                Type = c.Type,
                IsChampion = c.IsChampion,
                CommandSkill = c.Command,
                AgentSkill = c.Agent,
                EmissarySkill = c.Emissary,
                MageSkill = c.Mage,
                Health = 100,
                MaxHealth = 100,
                Stealth = c.Stealth,
                ChallengeRank = c.Challenge,
                LocationHex = $"{c.Q},{c.R}",
                IsDead = false,
                IsKidnapped = false
            };
            _db.Characters.Add(ch);
            foreach (var sid in c.SpellIds)
                _db.Spells.Add(new Spell
                {
                    Id = Guid.NewGuid().ToString(),
                    CharacterId = ch.Id,
                    SpellId = sid,
                    IsKnown = true,
                    IsLost = SpellCatalog.Get(sid)?.IsLost ?? false,
                    Rank = c.SpellRanks.TryGetValue(sid, out var rank) ? rank : 0
                });
            // Artefactos iniciales (Game 299 Turn 0): catálogo data.Artifacts + asignación por personaje.
            foreach (var aid in c.ArtifactIds)
            {
                data.Artifacts.TryGetValue(aid, out var ainfo);
                _db.Artifacts.Add(new Artifact
                {
                    Id = Guid.NewGuid().ToString(),
                    NationId = nation.Id,
                    Name = ainfo?.Name ?? $"Artifact #{aid}",
                    Type = ainfo?.Type ?? "misc",
                    Alignment = ainfo?.Alignment ?? "none",
                    Bonus = 0,
                    LocationHex = null,
                    HeldByCharacterId = ch.Id,
                    IsAtCapital = false
                });
            }
        }
        await _db.SaveChangesAsync();

        // ── Top commander leads the nation's largest army (if leaderless) ──
        // El módulo puede fijar el mando exacto (commands:"Q,R", p. ej. Woodmen, Northmen, Rohan, Dunadan).
        // Si en el hex hay armada además de ejército (p. ej. Girion II en 42,17), se asigna también la armada.
        foreach (var nation in allNations)
        {
            var led = new HashSet<string>();
            foreach (var c in data.Characters.Where(c => nationsBySlug.TryGetValue(c.NationSlug, out var nn) && nn.Id == nation.Id && c.CommandsAt != null))
            {
                var boss = _db.Characters.Local.FirstOrDefault(ch => ch.NationId == nation.Id && ch.Name == c.Name);
                var army = _db.Armies.Local.FirstOrDefault(a => a.NationId == nation.Id && a.LocationHex == c.CommandsAt && a.CommanderId == null);
                if (boss != null && army != null)
                {
                    army.CommanderId = boss.Id;
                    boss.ArmyId = army.Id;
                    boss.LocationHex = army.LocationHex;
                    led.Add(boss.Id);
                    var navy = _db.Navies.Local.FirstOrDefault(n => n.NationId == nation.Id && n.LocationHex == c.CommandsAt && n.CommanderId == null);
                    if (navy != null)
                        navy.CommanderId = boss.Id;
                }
                else if (boss != null)
                {
                    // Sin ejército en el hex (solo armada): asignar mando naval directo.
                    var navyOnly = _db.Navies.Local.FirstOrDefault(n => n.NationId == nation.Id && n.LocationHex == c.CommandsAt && n.CommanderId == null);
                    if (navyOnly != null)
                    {
                        navyOnly.CommanderId = boss.Id;
                        boss.LocationHex = navyOnly.LocationHex;
                        led.Add(boss.Id);
                    }
                }
            }
            var boss2 = _db.Characters.Local
                .Where(ch => ch.NationId == nation.Id && !led.Contains(ch.Id))
                .OrderByDescending(ch => ch.CommandSkill)
                .FirstOrDefault();
            var main = _db.Armies.Local
                .Where(a => a.NationId == nation.Id && a.CommanderId == null)
                .OrderByDescending(a => a.HeavyCavalry + a.LightCavalry + a.HeavyInfantry
                    + a.LightInfantry + a.Archers + a.MenAtArms)
                .FirstOrDefault();
            if (boss2 != null && main != null)
            {
                main.CommanderId = boss2.Id;
                boss2.ArmyId = main.Id;
                boss2.LocationHex = main.LocationHex;
            }
        }

        // ── Assign confirmed players to played nations (1:1 mezclado) ──
        // Las PNJ quedan sin jugador y pueden incorporarse luego (nación libre).
        var confirmedPlayers = game.Players.Where(p => p.IsReady).ToList();
        var playable = playedSlugs.Select(s => nationsBySlug[s]).OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < confirmedPlayers.Count; i++)
            confirmedPlayers[i].NationId = playable[i % playable.Count].Id;

        // ── Initial relations: same allegiance tolerated, rest neutral ──
        SeedInitialRelations(game.Id, allNations);

        // ── Start the game ──
        _db.Turns.Add(new Turn
        {
            Id = Guid.NewGuid().ToString(),
            GameId = game.Id,
            Number = 1,
            Status = "orders_open",
            Season = "summer",
            Deadline = DateTime.UtcNow.AddDays(7)
        });
        game.Status = "active";
        game.CurrentTurn = 1;
        game.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return allNations.Count;
    }

    private static int CalculateMapRadius(int totalNations)
    {
        // Each nation needs ~19 hexes of space (radius-1 circle)
        // Total hexes in grid ~ 3R² + 3R + 1
        // Solve: 3R² ≈ totalNations * 19 → R ≈ sqrt(totalNations * 19 / 3)
        var minRadius = 6;
        var maxRadius = 16;
        var radius = (int)Math.Ceiling(Math.Sqrt(totalNations * 19.0 / 3.0));
        return Math.Clamp(radius, minRadius, maxRadius);
    }

    private static string ComputeNationStartHex(int index, int total, int mapRadius, string allegiance)
    {
        // Spread nations evenly around the map edge
        // Free peoples on one half, dark servants on the other, neutrals near center
        double angle;
        int radius;

        if (allegiance == "neutral")
        {
            // Neutrals near the center
            angle = 2 * Math.PI * index / total;
            radius = Math.Max(3, mapRadius / 3);
        }
        else
        {
            // Main alliances: spread around the edge
            angle = 2 * Math.PI * index / total;
            radius = Math.Max(mapRadius - 2, 4);
        }

        int q = (int)Math.Round(radius * Math.Cos(angle));
        int r = (int)Math.Round(radius * Math.Sin(angle));

        // Clamp to valid hex coordinates
        int rMin = Math.Max(-mapRadius, -q - mapRadius);
        int rMax = Math.Min(mapRadius, -q + mapRadius);
        r = Math.Clamp(r, rMin, rMax);

        return $"{q},{r}";
    }

    private static string GetAdjacentHex(string hex, Random rng)
    {
        var parts = hex.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var q) || !int.TryParse(parts[1], out var r))
            return hex;

        // Hex cube directions: 6 adjacent hexes
        var directions = new (int dq, int dr)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)
        };

        var dir = directions[rng.Next(directions.Length)];
        return $"{q + dir.dq},{r + dir.dr}";
    }

    private static void CreateAdaptiveMap(string gameId, string gameTypeId, int size, MepbmDbContext db)
    {
        for (int q = -size; q <= size; q++)
        {
            var rMin = Math.Max(-size, -q - size);
            var rMax = Math.Min(size, -q + size);
            for (int r = rMin; r <= rMax; r++)
            {
                db.HexTiles.Add(new HexTile
                {
                    Id = Guid.NewGuid().ToString(),
                    GameId = gameId,
                    GameTypeId = gameTypeId,
                    Q = q,
                    R = r,
                    Terrain = GetTerrainForHex(q, r)
                });
            }
        }
    }

    private static void CreateInitialMap(string gameId, string gameTypeId, MepbmDbContext db)
    {
        const int size = 20;
        for (int q = -size; q <= size; q++)
        {
            var rMin = Math.Max(-size, -q - size);
            var rMax = Math.Min(size, -q + size);
            for (int r = rMin; r <= rMax; r++)
            {
                db.HexTiles.Add(new HexTile
                {
                    Id = Guid.NewGuid().ToString(),
                    GameId = gameId,
                    GameTypeId = gameTypeId,
                    Q = q,
                    R = r,
                    Terrain = GetTerrainForHex(q, r)
                });
            }
        }
    }

    private static string GetTerrainForHex(int q, int r)
    {
        var noise = Math.Sin(q * 0.5) * Math.Cos(r * 0.3) + Math.Sin((q + r) * 0.2);

        if (noise > 0.7) return "mountains";
        if (noise > 0.3) return "forest";
        if (noise > -0.3) return "plains";
        if (noise > -0.7) return "rough";
        return "swamp";
    }

    private static string GetCurrentSeason()
    {
        var month = DateTime.UtcNow.Month;
        if (month >= 3 && month <= 5) return "spring";
        if (month >= 6 && month <= 8) return "summer";
        if (month >= 9 && month <= 11) return "autumn";
        return "winter";
    }
}

public record CreateGameRequest(string Name, string? GameTypeCode, int? MaxTurns, int? TurnIntervalDays, List<string>? PlayerEmails, List<string>? AdminEmails);
public record JoinGameRequest(string? NationId);
public record UpdateNationRequest(string NationId);
public record SetRelationRequest(string NationId, string TargetNationId, int Level);
public record AcceptGameRequest(string? WantsToPlayWithUserId);
public record AddPlayerRequest(string Email, bool IsAdmin = false);
