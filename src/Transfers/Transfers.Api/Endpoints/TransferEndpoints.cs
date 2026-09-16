using System.Diagnostics;
using System.Security.Claims;
using BankFlow.ServiceDefaults;
using Transfers.Application.Common;
using Transfers.Application.Transfers;
using Transfers.Domain.Transfers;

namespace Transfers.Api.Endpoints;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var transfers = endpoints.MapGroup("/api/v1/transfers")
            .RequireAuthorization(SecurityPolicies.Transfers).WithTags("Transfers");
        transfers.MapPost("", CreateAsync).WithName("CreateTransfer");
        transfers.MapGet("", ListAsync).WithName("ListTransfers");
        transfers.MapGet("/{transferId:guid}", GetByIdAsync).WithName("GetTransfer");
        transfers.MapPost("/{transferId:guid}/reversal", RequestReversalAsync).WithName("RequestTransferReversal");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateTransferRequest request, ClaimsPrincipal principal,
        TransferService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateTransferCommand(request.SourceAccountId,
            request.DestinationAccountId, request.ExternalReference, request.Method, request.Amount,
            request.Description, GetActor(principal), context.Request.Headers.Authorization.ToString(),
            context.TraceIdentifier), cancellationToken);
        return TransferEndpointResults.From(result, context, response => response.IsReplay
            ? Results.Ok(response.Transfer)
            : Results.Accepted($"/api/v1/transfers/{response.Transfer.Id}", response.Transfer));
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, Guid? accountId,
        TransferStatus? status, TransferService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(page ?? 1, pageSize ?? 20, accountId, status, cancellationToken);
        return TransferEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> GetByIdAsync(Guid transferId, TransferService service,
        HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(transferId, cancellationToken);
        return TransferEndpointResults.From(result, context, Results.Ok);
    }

    private static async Task<IResult> RequestReversalAsync(Guid transferId, RequestReversalRequest request,
        ClaimsPrincipal principal, TransferService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.RequestReversalAsync(new RequestReversalCommand(transferId,
            request.RowVersion, request.Reason, GetActor(principal), context.TraceIdentifier), cancellationToken);
        return TransferEndpointResults.From(result, context, value => Results.Accepted(value: value));
    }

    private static string GetActor(ClaimsPrincipal principal) =>
        principal.FindFirst("sub")?.Value ?? principal.Identity?.Name ?? "authenticated-user";
}

public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId,
    string ExternalReference, TransferMethod Method, decimal Amount, string Description);
public sealed record RequestReversalRequest(string RowVersion, string Reason);

internal static class TransferEndpointResults
{
    public static IResult From<T>(TransfersResult<T> result, HttpContext context, Func<T, IResult> success)
    {
        if (result.IsSuccess)
        {
            return success(result.Value!);
        }

        var error = result.Error!;
        var (status, title) = error.Code switch
        {
            TransfersErrorCode.Validation => (422, "Dados inválidos"),
            TransfersErrorCode.NotFound => (404, "Transferência não encontrada"),
            TransfersErrorCode.Conflict => (409, "Operação não permitida"),
            TransfersErrorCode.DuplicateExternalReference => (409, "Referência duplicada"),
            TransfersErrorCode.ConcurrencyConflict => (409, "Conflito de concorrência"),
            TransfersErrorCode.AccountsUnavailable => (503, "Serviço de Contas indisponível"),
            _ => (500, "Erro inesperado")
        };
        return Results.Problem(statusCode: status, title: title, detail: error.Message,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier });
    }
}
