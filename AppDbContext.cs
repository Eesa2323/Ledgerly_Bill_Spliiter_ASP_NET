using BillSplitter.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillSplitter;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    // These sets make the financial model explicit and keep queries close to the database boundary.
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<ExpenseInvitation> ExpenseInvitations => Set<ExpenseInvitation>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Four decimal places preserve sub-penny precision during conversions and settlement calculations.
        builder.Entity<Expense>().Property(x => x.Amount).HasPrecision(19, 4);
        builder.Entity<ExpenseSplit>().Property(x => x.Amount).HasPrecision(19, 4);
        builder.Entity<Budget>().Property(x => x.Limit).HasPrecision(19, 4);
        // Composite keys prevent duplicate memberships and duplicate users in one expense split.
        builder.Entity<GroupMember>().HasKey(x => new { x.GroupId, x.UserId });
        builder.Entity<ExpenseSplit>().HasKey(x => new { x.ExpenseId, x.UserId });
        builder.Entity<ExpenseInvitation>().HasIndex(x => new { x.ExpenseId, x.InvitedUserId }).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<Expense>().HasOne(x => x.PaidByUser).WithMany(x => x.PaidExpenses).HasForeignKey(x => x.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExpenseSplit>().HasOne(x => x.User).WithMany(x => x.Splits).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ExpenseInvitation>().HasOne(x => x.InvitedUser).WithMany(x => x.ExpenseInvitations).HasForeignKey(x => x.InvitedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
