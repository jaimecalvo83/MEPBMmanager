using System.Security.Claims;
using MEPBMmanager.Api.Services;
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

        var games = await _db.Games
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
                gameTypeCode = g.GameType.Code
            })
            .ToListAsync();

        return Ok(new { games });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateGameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Game name is required" });

        var code = string.IsNullOrWhiteSpace(request.GameTypeCode) ? "1650" : request.GameTypeCode;
        var gameType = await _db.GameTypes.FirstOrDefaultAsync(g => g.Code == code)
                       ?? await _db.GameTypes.FirstOrDefaultAsync(g => g.Code == "1650");
        if (gameType == null)
            return BadRequest(new { error = "Unknown game type" });

        var game = new Game
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            GameTypeId = gameType.Id,
            MaxTurns = request.MaxTurns ?? 200,
            TurnIntervalDays = request.TurnIntervalDays ?? 14
        };

        _db.Games.Add(game);

        // Create nations from scenario templates (copiar todos los campos enriquecidos)
        var templates = await _db.NationTemplates.Where(t => t.GameTypeId == gameType.Id).ToListAsync();
        var createdNations = new List<Nation>();
        foreach (var t in templates)
        {
            var nation = new Nation
            {
                Id = Guid.NewGuid().ToString(),
                GameId = game.Id,
                Name = t.Name,
                Allegiance = t.Allegiance,
                Color = t.Color,
                StartHex = t.StartHex,
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

            // PC capital para cada nación
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
                HasPort = t.CapitalHasPort
            });

            // PC fronteriza para cada nación
            _db.PopulationCentres.Add(new PopulationCentre
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = t.BorderTownName,
                Size = t.BorderTownSize,
                LocationHex = t.StartHex,
                Loyalty = 55,
                Production = 100,
                Stores = 200,
                IsCapital = false,
                HasHarbour = false,
                HasPort = false
            });
        }

        // Ejércitos iniciales para cada nación (desde plantilla)
        foreach (var nation in createdNations)
        {
            var tmpl = templates.FirstOrDefault(t => t.Name == nation.Name);
            _db.Armies.Add(new Army
            {
                Id = Guid.NewGuid().ToString(),
                NationId = nation.Id,
                Name = "Main Army",
                LocationHex = nation.StartHex,
                HeavyCavalry = tmpl?.StartingHeavyCavalry ?? 0,
                LightCavalry = tmpl?.StartingLightCavalry ?? 0,
                HeavyInfantry = tmpl?.StartingHeavyInfantry ?? 0,
                LightInfantry = tmpl?.StartingLightInfantry ?? 0,
                Archers = tmpl?.StartingArchers ?? 0,
                MenAtArms = tmpl?.StartingMenAtArms ?? 0,
                WeaponRank = tmpl?.StartingWeaponRank ?? 10,
                ArmourRank = tmpl?.StartingArmourRank ?? 10,
                Morale = tmpl?.StartingMorale ?? 30,
                Training = tmpl?.StartingTraining ?? 10,
                Food = 2000,
                WarMachines = 0,
                IsOnManoeuvres = false
            });
        }

        // Create initial hex map tagged with the scenario
        CreateInitialMap(game.Id, gameType.Id, _db);

        // Creador es admin de la partida
        var creatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _db.Players.Add(new Player
        {
            Id = Guid.NewGuid().ToString(),
            UserId = creatorId,
            GameId = game.Id,
            Role = "admin"
        });

        await _db.SaveChangesAsync();

        return StatusCode(201, new { game = new { game.Id, game.Name, gameTypeCode = gameType.Code, game.Status } });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Get(string id)
    {
        var game = await _db.Games
            .Include(g => g.Nations)
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Include(g => g.Turns)
            .Include(g => g.HexTiles)
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return NotFound(new { error = "Game not found" });

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
                .Select(h => new { h.Id, h.Q, h.R, h.Terrain, h.OwnerId })
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

        if (game.Status != "setup")
            return BadRequest(new { error = "Game has already started" });

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
            NationId = request.NationId
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        return StatusCode(201, new { player = new { player.Id, player.UserId, player.GameId, player.NationId, player.Role, player.IsReady, player.JoinedAt } });
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

    [HttpPost("{id}/start")]
    [Authorize]
    public async Task<IActionResult> Start(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "setup")
            return BadRequest(new { error = "Game has already started" });

        var isAdmin = game.Players.Any(p => p.UserId == userId && p.Role == "admin");
        if (!isAdmin)
            return Forbid();

        if (game.Players.Count < 1)
            return BadRequest(new { error = "Need at least 1 player to start" });

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

        game.Players.First().IsReady = true;

        var nations = await _db.Nations.Where(n => n.GameId == id).ToListAsync();
        var takenNations = game.Players.Where(p => p.NationId != null).Select(p => p.NationId!).ToHashSet();
        var rng = new Random();

        foreach (var player in game.Players)
        {
            if (player.NationId == null)
            {
                player.NationId = nations.FirstOrDefault(n => !takenNations.Contains(n.Id))?.Id;
                if (player.NationId != null) takenNations.Add(player.NationId);
            }
            if (player.NationId == null) continue;
            InitializeNationAtStart(game.Id, player.NationId, rng);
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Game started" });
    }

    [HttpPost("{id}/process-turn")]
    [Authorize]
    public async Task<IActionResult> ProcessTurn(string id, [FromServices] TurnProcessor processor)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var game = await _db.Games.Include(g => g.Players).FirstOrDefaultAsync(g => g.Id == id);
        if (game == null)
            return NotFound(new { error = "Game not found" });

        if (game.Status != "active")
            return BadRequest(new { error = "Game is not active" });

        var isAdmin = game.Players.Any(p => p.UserId == userId && p.Role == "admin");
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
        var ruler = CreateCharacter(nationId, "Ruler of the Realm", "commander", command: 13, agent: 10, emissary: 10, mage: 10, captain: true, hex: capitalHex, champion: true);
        _db.Characters.Add(ruler);

        var commander2 = CreateCharacter(nationId, "Commander of the Guard", "commander", command: 11, agent: 8, emissary: 8, mage: 8, captain: false, hex: capitalHex);
        _db.Characters.Add(commander2);

        var commander3 = CreateCharacter(nationId, "Marshal of the Field", "commander", command: 10, agent: 7, emissary: 7, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(commander3);

        var agent = CreateCharacter(nationId, "Master of Whispers", "agent", command: 7, agent: 12, emissary: 9, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(agent);

        var emissary = CreateCharacter(nationId, "Voice of the Crown", "emissary", command: 7, agent: 8, emissary: 12, mage: 7, captain: false, hex: capitalHex);
        _db.Characters.Add(emissary);

        var mage = CreateCharacter(nationId, "Keeper of Lore", "mage", command: 7, agent: 7, emissary: 8, mage: 12, captain: false, hex: capitalHex);
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
    public async Task<IActionResult> State(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var player = await _db.Players
            .Include(p => p.Nation)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.GameId == id);

        if (player == null)
            return StatusCode(403, new { error = "Not a player in this game" });

        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id);
        var currentTurn = await _db.Turns
            .Where(t => t.GameId == id && t.Number == game!.CurrentTurn)
            .OrderByDescending(t => t.Number)
            .FirstOrDefaultAsync();
        var characters = await _db.Characters.Where(c => c.NationId == player.NationId).ToListAsync();
        var armies = await _db.Armies.Where(a => a.NationId == player.NationId).ToListAsync();
        var navies = await _db.Navies.Where(n => n.NationId == player.NationId).ToListAsync();
        var populationCentres = await _db.PopulationCentres.Where(p => p.NationId == player.NationId).ToListAsync();
        var hexTiles = await _db.HexTiles.Where(h => h.GameId == id).ToListAsync();
        var relations = await _db.NationRelations.Where(nr => nr.NationId == player.NationId).ToListAsync();

        return Ok(new
        {
            game = new { game!.Id, game.Name, gameTypeCode = game.GameType?.Code, game.Status, game.CurrentTurn, game.MaxTurns, game.TurnIntervalDays },
            player = new { player.Id, player.UserId, player.GameId, player.NationId, player.IsReady, player.JoinedAt },
            nation = player.Nation == null ? null : new
            {
                player.Nation.Id,
                player.Nation.Name,
                player.Nation.Allegiance,
                player.Nation.Color,
                player.Nation.TaxRate,
                player.Nation.Gold,
                player.Nation.Food,
                player.Nation.Timber,
                player.Nation.Leather,
                player.Nation.Bronze,
                player.Nation.Steel,
                player.Nation.Mithril,
                player.Nation.Mounts
            },
            currentTurn = currentTurn == null ? null : new { currentTurn.Id, currentTurn.Number, currentTurn.Status, currentTurn.Season, currentTurn.Deadline },
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
                c.ArmyId
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
                a.WeaponRank,
                a.ArmourRank,
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
            hexTiles = hexTiles.Select(h => new { h.Id, h.Q, h.R, h.Terrain, h.OwnerId }),
            relations = relations.Select(rel => new { rel.Id, rel.TargetNationId, rel.Level })
        });
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

public record CreateGameRequest(string Name, string? GameTypeCode, int? MaxTurns, int? TurnIntervalDays);
public record JoinGameRequest(string? NationId);
