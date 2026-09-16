using BankFlow.Contracts.Events;
using BankFlow.Contracts.Messaging;
using Transfers.Application.Abstractions;
using Transfers.Application.Common;
using Transfers.Domain.Transfers;

namespace Transfers.Application.Transfers;

public sealed class TransferService(ITransferRepository repository, IAccountCatalog accountCatalog,
    IIntegrationOutbox integrationOutbox, TimeProvider timeProvider)
{
    public async Task<TransfersResult<CreateTransferResponse>> CreateAsync(
        CreateTransferCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Authorization))
        {
            return Validation<CreateTransferResponse>("A credencial usada para validar as contas não foi recebida.");
        }

        Transfer transfer;
        try
        {
            transfer = Transfer.Create(Guid.CreateVersion7(), command.SourceAccountId,
                command.DestinationAccountId, command.ExternalReference, command.Method,
                command.Amount, command.Description, command.CreatedBy, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation<CreateTransferResponse>(exception.Message);
        }

        var existing = await repository.GetByExternalReferenceAsync(transfer.ExternalReference, cancellationToken);
        if (existing is not null)
        {
            return Matches(existing, command)
                ? TransfersResult.Success(new CreateTransferResponse(TransferResponse.From(existing), true))
                : TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.DuplicateExternalReference,
                    "A referência externa já pertence a outra transferência.");
        }

        var lookups = await Task.WhenAll(
            accountCatalog.GetAccountAsync(command.SourceAccountId, command.Authorization, cancellationToken),
            accountCatalog.GetAccountAsync(command.DestinationAccountId, command.Authorization, cancellationToken));

        if (lookups.Any(item => item.Status == AccountLookupStatus.Unavailable))
        {
            return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.AccountsUnavailable,
                lookups.First(item => item.Status == AccountLookupStatus.Unavailable).Detail ?? "O serviço de Contas está indisponível.");
        }

        if (lookups.Any(item => item.Status == AccountLookupStatus.NotFound || item.Account is null))
        {
            return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.Conflict,
                "A conta de origem ou destino não foi encontrada.");
        }

        var source = lookups[0].Account!;
        var destination = lookups[1].Account!;
        if (!string.Equals(source.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.Conflict, "A conta de origem não está ativa.");
        }

        if (string.Equals(destination.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.Conflict, "A conta de destino está encerrada.");
        }

        if (source.Balance < transfer.Amount)
        {
            return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.Conflict, "Saldo disponível insuficiente.");
        }

        repository.Add(transfer);
        integrationOutbox.Enqueue(new TransferRequestedV1(Guid.CreateVersion7(), transfer.CreatedAtUtc,
            command.CorrelationId, transfer.Id, transfer.SourceAccountId, transfer.DestinationAccountId,
            transfer.Amount, (TransferMethodV1)transfer.Method, transfer.Description),
            MessagingTopology.TransferRequestedRoutingKey);

        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == TransferSaveOutcome.Success)
        {
            return TransfersResult.Success(new CreateTransferResponse(TransferResponse.From(transfer), false));
        }

        if (outcome == TransferSaveOutcome.DuplicateExternalReference)
        {
            existing = await repository.GetByExternalReferenceAsync(transfer.ExternalReference, cancellationToken);
            return existing is not null && Matches(existing, command)
                ? TransfersResult.Success(new CreateTransferResponse(TransferResponse.From(existing), true))
                : TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.DuplicateExternalReference,
                    "A referência externa já foi utilizada com outro conteúdo.");
        }

        return TransfersResult.Failure<CreateTransferResponse>(TransfersErrorCode.ConcurrencyConflict,
            "A transferência recebeu uma alteração concorrente.");
    }

    public async Task<TransfersResult<TransferResponse>> RequestReversalAsync(
        RequestReversalCommand command, CancellationToken cancellationToken)
    {
        var version = DecodeRowVersion(command.RowVersion);
        if (!version.IsSuccess)
        {
            return TransfersResult.Failure<TransferResponse>(version.Error!.Code, version.Error.Message);
        }

        var transfer = await repository.GetByIdAsync(command.TransferId, true, cancellationToken);
        if (transfer is null)
        {
            return NotFound<TransferResponse>();
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            transfer.RequestReversal(command.Reason, command.RequestedBy, now);
        }
        catch (ArgumentException exception)
        {
            return Validation<TransferResponse>(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return TransfersResult.Failure<TransferResponse>(TransfersErrorCode.Conflict, exception.Message);
        }

        repository.SetExpectedRowVersion(transfer, version.Value!);
        integrationOutbox.Enqueue(new TransferReversalRequestedV1(Guid.CreateVersion7(), now,
            command.CorrelationId, transfer.Id, transfer.ReversalReason!, command.RequestedBy),
            MessagingTopology.TransferReversalRequestedRoutingKey);
        return await repository.SaveChangesAsync(cancellationToken) == TransferSaveOutcome.Success
            ? TransfersResult.Success(TransferResponse.From(transfer))
            : TransfersResult.Failure<TransferResponse>(TransfersErrorCode.ConcurrencyConflict, "A versão informada está desatualizada.");
    }

    public async Task<TransfersResult<TransferResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await repository.GetByIdAsync(id, false, cancellationToken);
        return transfer is null ? NotFound<TransferResponse>() : TransfersResult.Success(TransferResponse.From(transfer));
    }

    public async Task<TransfersResult<PagedResult<TransferResponse>>> ListAsync(int page, int pageSize,
        Guid? accountId, TransferStatus? status, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || accountId == Guid.Empty
            || (status is not null && !Enum.IsDefined(status.Value)))
        {
            return Validation<PagedResult<TransferResponse>>("Os filtros ou a paginação são inválidos.");
        }

        var (items, count) = await repository.ListAsync(page, pageSize, accountId, status, cancellationToken);
        return TransfersResult.Success(new PagedResult<TransferResponse>(
            items.Select(TransferResponse.From).ToArray(), page, pageSize, count));
    }

    private static bool Matches(Transfer transfer, CreateTransferCommand command) =>
        transfer.SourceAccountId == command.SourceAccountId
        && transfer.DestinationAccountId == command.DestinationAccountId
        && transfer.Method == command.Method && transfer.Amount == command.Amount;

    private static TransfersResult<byte[]> DecodeRowVersion(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value ?? string.Empty);
            return bytes.Length == 8 ? TransfersResult.Success(bytes) : Validation<byte[]>("A versão da transferência é inválida.");
        }
        catch (FormatException)
        {
            return Validation<byte[]>("A versão da transferência é inválida.");
        }
    }

    private static TransfersResult<T> Validation<T>(string message) => TransfersResult.Failure<T>(TransfersErrorCode.Validation, message);
    private static TransfersResult<T> NotFound<T>() => TransfersResult.Failure<T>(TransfersErrorCode.NotFound, "Transferência não encontrada.");
}
