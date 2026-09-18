using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MEPBMmanager.Domain.Entities;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MEPBMmanager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MepbmDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(MepbmDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 6)
        {
            return BadRequest(new { error = "Email, username and password (min 6 chars) are required" });
        }

        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
            return Conflict(new { error = "Email already registered" });

        var usernameExists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (usernameExists)
            return Conflict(new { error = "Username already taken" });

        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == "game_user");

        var lang = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "en" : request.PreferredLanguage;

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = request.Email,
            Username = request.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            RoleId = defaultRole?.Id ?? "game_user",
            PreferredLanguage = lang
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.Username, user.RoleId, user.PreferredLanguage } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(new { error = "Invalid credentials" });

        var token = GenerateToken(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.Username, user.RoleId, user.PreferredLanguage, role = user.Role?.Name } });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        return Ok(new { user = new { user.Id, user.Email, user.Username, user.RoleId, user.PreferredLanguage, role = user.Role?.Name } });
    }

    [HttpPatch("language")]
    [Authorize]
    public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var lang = request.Language?.ToLower() == "es" ? "es" : "en";
        user.PreferredLanguage = lang;
        await _db.SaveChangesAsync();

        return Ok(new { user.PreferredLanguage });
    }

    private string GenerateToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.RoleId),
            new Claim("preferred_language", user.PreferredLanguage)
        };

        var expirationInDays = _config.GetValue<int>("Jwt:ExpirationInDays", 7);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expirationInDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record RegisterRequest(string Email, string Username, string Password, string? PreferredLanguage);
public record LoginRequest(string Email, string Password);
public record UpdateLanguageRequest(string? Language);
