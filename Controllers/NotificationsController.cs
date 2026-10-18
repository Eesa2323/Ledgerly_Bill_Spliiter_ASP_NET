using BillSplitter.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/notifications")]
public sealed class NotificationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        // Notifications are scoped to the recipient, so one user cannot inspect another user's invitations.
        var notifications = await db.ExpenseInvitations.AsNoTracking()
            .Where(invitation => invitation.InvitedUserId == UserId && invitation.Status == ExpenseInvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .Select(invitation => new
            {
                invitation.Id,
                invitation.CreatedAtUtc,
                invitation.ExpenseId,
                invitation.Expense.Description,
                invitation.Expense.Amount,
                invitation.Expense.Currency,
                GroupName = invitation.Expense.Group == null ? "Direct expense" : invitation.Expense.Group.Name,
                PayerName = invitation.Expense.PaidByUser.DisplayName,
                SplitAmount = invitation.Expense.Splits.Where(split => split.UserId == invitation.InvitedUserId).Select(split => split.Amount).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return Ok(notifications);
    }

    [HttpPost("{notificationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await FindPending(notificationId, cancellationToken);
        if (notification is null) return NotFound();
        notification.Status = ExpenseInvitationStatus.Accepted;
        notification.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Dismiss(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await FindPending(notificationId, cancellationToken);
        if (notification is null) return NotFound();
        notification.Status = ExpenseInvitationStatus.Dismissed;
        notification.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<ExpenseInvitation?> FindPending(Guid notificationId, CancellationToken cancellationToken) => db.ExpenseInvitations.SingleOrDefaultAsync(notification => notification.Id == notificationId && notification.InvitedUserId == UserId && notification.Status == ExpenseInvitationStatus.Pending, cancellationToken);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}