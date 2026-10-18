using BillSplitter.Contracts;
using BillSplitter.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/groups/{groupId:guid}/expenses")]
public sealed class ExpensesController(AppDbContext db, IValidator<CreateExpenseRequest> validator) : ControllerBase
{
    [HttpGet]
    // Loading membership in the query prevents one group from becoming an information leak.
    public async Task<IActionResult> List(Guid groupId, CancellationToken cancellationToken) => Ok(await db.Expenses.AsNoTracking().Where(x => x.GroupId == groupId && x.Group!.Members.Any(m => m.UserId == UserId)).Select(x => new { x.Id, x.Description, x.Amount, x.Currency, x.SpentAtUtc, x.PaidByUserId, PayerUsername = x.PaidByUser.UserName, Splits = x.Splits.Select(s => new { s.UserId, Username = s.User.UserName, s.Amount, s.SettledAtUtc }) }).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(Guid groupId, CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        // FluentValidation checks the arithmetic first; membership checks then bind every split to this group.
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var memberIds = await db.GroupMembers.Where(x => x.GroupId == groupId).Select(x => x.UserId).ToHashSetAsync(cancellationToken);
        if (!memberIds.Contains(UserId) || request.Splits.Any(x => !memberIds.Contains(x.UserId))) return Forbid();
        var expense = new Expense { GroupId = groupId, PaidByUserId = UserId, Description = request.Description.Trim(), Amount = request.Amount, Currency = request.Currency.ToUpperInvariant(), SpentAtUtc = request.SpentAtUtc, Splits = request.Splits.Select(x => new ExpenseSplit { UserId = x.UserId, Amount = x.Amount }).ToList() };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        // Return a flat response instead of the tracked entity, whose navigation properties form a cycle.
        return Created($"/api/groups/{groupId}/expenses/{expense.Id}", new { expense.Id, expense.Description, expense.Amount, expense.Currency, expense.SpentAtUtc, expense.GroupId, expense.PaidByUserId });
    }

    [HttpPost("{expenseId:guid}/settle")]
    public async Task<IActionResult> Settle(Guid groupId, Guid expenseId, CancellationToken cancellationToken)
    {
        // A participant can settle only their own share of an expense in a group they belong to.
        var split = await db.ExpenseSplits.Include(x => x.Expense).SingleOrDefaultAsync(x => x.ExpenseId == expenseId && x.Expense.GroupId == groupId && x.UserId == UserId, cancellationToken);
        if (split is null) return NotFound();
        if (split.Expense.PaidByUserId == UserId) return BadRequest("The payer does not need to settle their own share.");
        if (split.SettledAtUtc is null)
        {
            split.SettledAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}
