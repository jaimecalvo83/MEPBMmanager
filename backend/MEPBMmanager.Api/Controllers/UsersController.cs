using System.Security.Claims;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MepbmDbContext _db;

    public UsersController(MepbmDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _db.Users
            .Select(u => new { u.Id, u.Email, u.Username })
            .OrderBy(u => u.Username)
            .ToListAsync();

        return Ok(new { users });
    }
}
