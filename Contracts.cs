namespace BillSplitter.Contracts;

// Request and response records keep the public API contract separate from EF entities.
public sealed record RegisterRequest(string Username, string Email, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
public sealed record CreateGroupRequest(string Name, IReadOnlyCollection<string> MemberUsernames);
public sealed record AddGroupMemberRequest(string Username);
public sealed record CreateExpenseRequest(string Description, decimal Amount, string Currency, DateTime SpentAtUtc, IReadOnlyCollection<ExpenseSplitRequest> Splits);
public sealed record CreateDirectExpenseRequest(string Description, decimal Amount, string Currency, DateTime SpentAtUtc, string Username, decimal OtherUserShare);
public sealed record ExpenseSplitRequest(Guid UserId, decimal Amount);
public sealed record CreateBudgetRequest(string Name, decimal Limit, string Currency, DateOnly StartsOn, DateOnly EndsOn);
public sealed record ConversionRequest(decimal Amount, string From, string To);
public sealed record ConversionResponse(decimal Amount, string From, string To, decimal Rate, decimal ConvertedAmount);
