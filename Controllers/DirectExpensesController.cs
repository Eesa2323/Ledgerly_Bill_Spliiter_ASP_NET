using BillSplitter.Contracts;
using BillSplitter.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/expenses")]
public sealed class DirectExpensesController(AppDbContext db, UserManager<AppUser> users, IValidator<CreateDirectExpenseRequest> validator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        // Direct expenses have no group, so both participants can still view their own records here.
        var expenses = await db.Expenses.AsNoTracking()
            .Where(expense => expense.GroupId == null && (expense.PaidByUserId == UserId || expense.Splits.Any(split => split.UserId == UserId)))
            .OrderByDescending(expense => expense.SpentAtUtc)
            .Select(expense => new { expense.Id, expense.Description, expense.Amount, expense.Currency, expense.SpentAtUtc, PayerUsername = expense.PaidByUser.UserName, Splits = expense.Splits.Select(split => new { split.UserId, Username = split.User.UserName, split.Amount, split.SettledAtUtc }) })
            .ToListAsync(cancellationToken);
        return Ok(expenses);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDirectExpenseRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var target = await users.Users.SingleOrDefaultAsync(user => user.UserName == request.Username.Trim(), cancellationToken);
        if (target is null) return NotFound("Username was not found.");
        if (target.Id == UserId) return BadRequest("Choose another username for a direct expense.");
        if (request.Amount <= 0 || request.OtherUserShare <= 0 || request.OtherUserShare >= request.Amount) return BadRequest("The other user's share must be greater than zero and less than the total.");
        var expense = new Expense
        {
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            SpentAtUtc = request.SpentAtUtc,
            PaidByUserId = UserId,
            Splits = [new ExpenseSplit { UserId = UserId, Amount = request.Amount - request.OtherUserShare }, new ExpenseSplit { UserId = target.Id, Amount = request.OtherUserShare }],
            Invitations = [new ExpenseInvitation { InvitedUserId = target.Id }]
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/expenses/{expense.Id}", new { expense.Id, expense.Description, expense.Amount, expense.Currency, Username = target.UserName });
    }

    [HttpPost("{expenseId:guid}/settle")]
    public async Task<IActionResult> Settle(Guid expenseId, CancellationToken cancellationToken)
    {
        // Direct expenses use the same per-split settlement rule as group expenses.
        var split = await db.ExpenseSplits.Include(item => item.Expense).SingleOrDefaultAsync(item => item.ExpenseId == expenseId && item.Expense.GroupId == null && item.UserId == UserId, cancellationToken);
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