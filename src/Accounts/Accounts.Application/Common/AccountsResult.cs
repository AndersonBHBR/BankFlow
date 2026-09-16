namespace Accounts.Application.Common;

public sealed record AccountsError(AccountsErrorCode Code, string Message);

public enum AccountsErrorCode
{
    Validation,
    NotFound,
    DuplicateResource,
    Conflict,
    ConcurrencyConflict
}

public sealed record AccountsResult<T>(T? Value, AccountsError? Error)
{
    public bool IsSuccess => Error is null;
}

public static class AccountsResult
{
    public static AccountsResult<T> Success<T>(T value) => new(value, null);
    public static AccountsResult<T> Failure<T>(AccountsErrorCode code, string message) =>
        new(default, new AccountsError(code, message));
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
