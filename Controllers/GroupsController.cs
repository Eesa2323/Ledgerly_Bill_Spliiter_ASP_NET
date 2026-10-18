using BillSplitter.Contracts;
using BillSplitter.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/groups")]
public sealed class GroupsController(AppDbContext db, UserManager<AppUser> users, IValidator<CreateGroupRequest> validator) : ControllerBase
{
    [HttpGet]
    // A user only sees groups where they are a member.
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Groups.AsNoTracking().Where(x => x.Members.Any(m => m.UserId == UserId)).Select(x => new { x.Id, x.Name, x.CreatedAtUtc, Members = x.Members.Select(m => new { m.UserId, Username = m.User.UserName, m.User.DisplayName, m.User.Email }) }).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        // The creator is always included, even if the browser sends an empty member list.
        var currentUser = await users.FindByIdAsync(UserId.ToString());
        var usernames = request.MemberUsernames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Append(currentUser?.UserName ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var validUsers = await users.Users.Where(x => x.UserName != null && usernames.Contains(x.UserName)).ToListAsync(cancellationToken);
        if (validUsers.Count != usernames.Length) return BadRequest("Every group member must be a registered username.");
        var group = new Group { Name = request.Name.Trim(), Members = validUsers.Select(user => new GroupMember { UserId = user.Id }).ToList() };
        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/groups/{group.Id}", new { group.Id, group.Name });
    }

    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid groupId, AddGroupMemberRequest request, CancellationToken cancellationToken)
    {
        // Only an existing group member can invite another registered username into the group.
        if (!await db.GroupMembers.AnyAsync(member => member.GroupId == groupId && member.UserId == UserId, cancellationToken)) return Forbid();
        var username = request.Username.Trim();
        var user = await users.Users.SingleOrDefaultAsync(candidate => candidate.UserName == username, cancellationToken);
        if (user is null) return NotFound("Username was not found.");
        if (await db.GroupMembers.AnyAsync(member => member.GroupId == groupId && member.UserId == user.Id, cancellationToken)) return Conflict("That username is already in the group.");
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = user.Id });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { user.Id, Username = user.UserName, user.DisplayName });
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name) ?? throw new UnauthorizedAccessException());
}
