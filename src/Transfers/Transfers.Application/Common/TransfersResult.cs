namespace Transfers.Application.Common;

public sealed record TransfersError(TransfersErrorCode Code, string Message);

public enum TransfersErrorCode
{
    Validation,
    NotFound,
    Conflict,
    DuplicateExternalReference,
    ConcurrencyConflict,
    AccountsUnavailable
}

public sealed record TransfersResult<T>(T? Value, TransfersError? Error)
{
    public bool IsSuccess => Error is null;
}

public static class TransfersResult
{
    public static TransfersResult<T> Success<T>(T value) => new(value, null);

    public static TransfersResult<T> Failure<T>(TransfersErrorCode code, string message) =>
        new(default, new TransfersError(code, message));
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
