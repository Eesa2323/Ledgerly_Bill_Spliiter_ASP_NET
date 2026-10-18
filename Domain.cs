using Microsoft.AspNetCore.Identity;

namespace BillSplitter.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
    // Username is the public identity used for search and group membership.
    public string DisplayName { get; set; } = string.Empty;
    public ICollection<GroupMember> GroupMemberships { get; set; } = [];
    public ICollection<Expense> PaidExpenses { get; set; } = [];
    public ICollection<ExpenseSplit> Splits { get; set; } = [];
    public ICollection<ExpenseInvitation> ExpenseInvitations { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<GroupMember> Members { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
}

public sealed class GroupMember
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Expense
{
    // Amount is decimal all the way through the domain; never use floating point for money.
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid PaidByUserId { get; set; }
    public AppUser PaidByUser { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime SpentAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ExpenseSplit> Splits { get; set; } = [];
    public ICollection<ExpenseInvitation> Invitations { get; set; } = [];
}

public enum ExpenseInvitationStatus
{
    Pending,
    Accepted,
    Dismissed
}

public sealed class ExpenseInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public Guid InvitedUserId { get; set; }
    public AppUser InvitedUser { get; set; } = null!;
    public ExpenseInvitationStatus Status { get; set; } = ExpenseInvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}

public sealed class ExpenseSplit
{
    public Guid ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime? SettledAtUtc { get; set; }
}

public sealed class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Limit { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    // A refresh token is single-use once rotation revokes it.
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
