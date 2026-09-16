using System.Diagnostics;
using System.Security.Claims;
using Accounts.Application.Accounts;
using Accounts.Application.Common;
using Accounts.Domain.Accounts;
using BankFlow.ServiceDefaults;

namespace Accounts.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/api/v1/accounts").WithTags("Accounts");
        accounts.MapPost("", CreateAsync).RequireAuthorization(SecurityPolicies.Accounts).WithName("CreateAccount");
        accounts.MapGet("", ListAsync).RequireAuthorization(SecurityPolicies.Authenticated).WithName("ListAccounts");
        accounts.MapGet("/{accountId:guid}", GetByIdAsync).RequireAuthorization(SecurityPolicies.Authenticated).WithName("GetAccount");
        accounts.MapGet("/by-pix-key", GetByPixKeyAsync).RequireAuthorization(SecurityPolicies.Authenticated).WithName("GetAccountByPixKey");
        accounts.MapPut("/{accountId:guid}", UpdateAsync).RequireAuthorization(SecurityPolicies.Accounts).WithName("UpdateAccount");
        accounts.MapPost("/{accountId:guid}/cash-in", CashInAsync).RequireAuthorization(SecurityPolicies.Accounts).WithName("CashIn");
        accounts.MapGet("/{accountId:guid}/ledger", ListLedgerAsync).RequireAuthorization(SecurityPolicies.Authenticated).WithName("ListLedger");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateAccountRequest request, ClaimsPrincipal principal,
        AccountService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateAccountCommand(request.Number, request.HolderId,
            request.HolderName, request.Document, request.PixKey, request.Type, request.InitialBalance,
            request.DailyTransferLimit, request.NightlyTransferLimit, GetActor(principal)), cancellationToken);
        return AccountEndpointResults.From(result, context,
            account => Results.Created($"/api/v1/accounts/{account.Id}", account));
    }

    private static async Task<IResult> UpdateAsync(Guid accountId, UpdateAccountRequest request,
        AccountService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(new UpdateAccountCommand(accountId,
            request.DailyTransferLimit, request.NightlyTransferLimit, request.Status, request.RowVersion), cancellationToken);
        return AccountEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> CashInAsync(Guid accountId, CashInRequest request,
        ClaimsPrincipal principal, AccountService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.CashInAsync(new CashInCommand(accountId, request.Amount,
            request.Description, GetActor(principal)), cancellationToken);
        return AccountEndpointResults.From(result, context,
            response => Results.Created($"/api/v1/accounts/{accountId}/ledger/{response.Entry.Id}", response));
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, string? search,
        AccountStatus? status, AccountService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(page ?? 1, pageSize ?? 20, search, status, cancellationToken);
        return AccountEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> GetByIdAsync(Guid accountId, AccountService service,
        HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(accountId, cancellationToken);
        return AccountEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> GetByPixKeyAsync(string pixKey, AccountService service,
        HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.GetByPixKeyAsync(pixKey, cancellationToken);
        return AccountEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> ListLedgerAsync(Guid accountId, int? page, int? pageSize,
        AccountService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ListLedgerAsync(accountId, page ?? 1, pageSize ?? 20, cancellationToken);
        return AccountEndpointResults.From(result, context, Results.Ok);
    }

    private static string GetActor(ClaimsPrincipal principal) =>
        principal.FindFirst("sub")?.Value ?? principal.Identity?.Name ?? "authenticated-user";
}

public sealed record CreateAccountRequest(string Number, string HolderId, string HolderName,
    string Document, string PixKey, AccountType Type, decimal InitialBalance,
    decimal DailyTransferLimit, decimal NightlyTransferLimit);
public sealed record UpdateAccountRequest(decimal DailyTransferLimit, decimal NightlyTransferLimit,
    AccountStatus Status, string RowVersion);
public sealed record CashInRequest(decimal Amount, string Description);

internal static class AccountEndpointResults
{
    public static IResult From<T>(AccountsResult<T> result, HttpContext context, Func<T, IResult> success)
    {
        if (result.IsSuccess)
        {
            return success(result.Value!);
        }

        var error = result.Error!;
        var (status, title) = error.Code switch
        {
            AccountsErrorCode.Validation => (422, "Dados inválidos"),
            AccountsErrorCode.NotFound => (404, "Conta não encontrada"),
            AccountsErrorCode.DuplicateResource => (409, "Conta ou chave Pix duplicada"),
            AccountsErrorCode.Conflict => (409, "Operação não permitida"),
            AccountsErrorCode.ConcurrencyConflict => (409, "Conflito de concorrência"),
            _ => (500, "Erro inesperado")
        };
        return Results.Problem(statusCode: status, title: title, detail: error.Message,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier });
    }
}
