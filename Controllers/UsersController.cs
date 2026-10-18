using BillSplitter.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/users")]
public sealed class UsersController(UserManager<AppUser> users) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        // Search is deliberately limited and returns only fields needed to select a group member.
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2) return Ok(Array.Empty<object>());
        var pattern = $"%{query.Trim()}%";
        var results = await users.Users.AsNoTracking()
            .Where(user => user.Id != UserId && EF.Functions.Like(user.UserName!, pattern))
            .OrderBy(user => user.UserName)
            .Take(10)
            .Select(user => new { user.Id, Username = user.UserName, user.DisplayName })
            .ToListAsync(cancellationToken);
        return Ok(results);
    }

    private Guid UserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
}