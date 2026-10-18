using BillSplitter.Contracts;
using FluentValidation;

namespace BillSplitter;

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    // Registration rules give clients useful feedback before Identity hashes the password.
    public RegisterValidator() { RuleFor(x => x.Username).NotEmpty().Length(3, 30).Matches("^[A-Za-z0-9_]+$").WithMessage("Username must be 3-30 characters and contain only letters, numbers, or underscores."); RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Password).MinimumLength(10); }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator() { RuleFor(x => x.Username).NotEmpty(); RuleFor(x => x.Password).NotEmpty(); }
}

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator()
    {
    // The total check is intentionally decimal-based and exact; a bill cannot silently lose a penny.
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(19, 4, true);
        RuleFor(x => x.Currency).Length(3).Must(x => x == x.ToUpperInvariant()).WithMessage("Currency must be uppercase.");
        RuleFor(x => x.Splits).NotEmpty().Must((request, splits) => splits.Sum(x => x.Amount) == request.Amount).WithMessage("Expense splits must add up exactly to the total amount.");
        RuleForEach(x => x.Splits).ChildRules(split => split.RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(19, 4, true));
    }
}

public sealed class CreateDirectExpenseValidator : AbstractValidator<CreateDirectExpenseRequest>
{
    public CreateDirectExpenseValidator()
    {
        // Direct expenses still use exact decimal money rules, but only validate the recipient's share here.
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Username).NotEmpty().Length(3, 30).Matches("^[A-Za-z0-9_]+$").WithMessage("Enter a valid username.");
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(19, 4, true);
        RuleFor(x => x.OtherUserShare).GreaterThan(0).PrecisionScale(19, 4, true).LessThan(x => x.Amount).WithMessage("The user's share must be less than the total amount.");
        RuleFor(x => x.Currency).Length(3).Must(x => x == x.ToUpperInvariant()).WithMessage("Currency must be uppercase.");
        RuleFor(x => x.SpentAtUtc).NotEmpty();
    }
}

public sealed class CreateBudgetValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(100); RuleFor(x => x.Limit).GreaterThan(0).PrecisionScale(19, 4, true); RuleFor(x => x.Currency).Length(3).Must(x => x == x.ToUpperInvariant()).WithMessage("Currency must be uppercase."); RuleFor(x => x).Must(x => x.EndsOn >= x.StartsOn).WithMessage("Budget end date must be on or after its start date."); }
}

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleForEach(x => x.MemberUsernames).NotEmpty().Length(3, 30).Matches("^[A-Za-z0-9_]+$").WithMessage("Group members must use valid usernames.");
    }
}
