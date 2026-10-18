using BillSplitter.Contracts;
using BillSplitter.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/groups/{groupId:guid}/budgets")]
public sealed class BudgetsController(AppDbContext db, IValidator<CreateBudgetRequest> validator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid groupId, CancellationToken cancellationToken) => Ok(await db.Budgets.AsNoTracking().Where(x => x.GroupId == groupId && x.Group.Members.Any(m => m.UserId == UserId)).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(Guid groupId, CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        // A budget belongs to a group, so creation is refused unless the caller is a member.
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        if (!await db.GroupMembers.AnyAsync(x => x.GroupId == groupId && x.UserId == UserId, cancellationToken)) return Forbid();
        var budget = new Budget { GroupId = groupId, Name = request.Name.Trim(), Limit = request.Limit, Currency = request.Currency.ToUpperInvariant(), StartsOn = request.StartsOn, EndsOn = request.EndsOn };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/groups/{groupId}/budgets/{budget.Id}", budget);
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}
