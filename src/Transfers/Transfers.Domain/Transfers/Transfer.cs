namespace Transfers.Domain.Transfers;

public sealed class Transfer
{
    private Transfer()
    {
    }

    private Transfer(Guid id, Guid sourceAccountId, Guid destinationAccountId,
        string externalReference, TransferMethod method, decimal amount, string description,
        string createdBy, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Number = $"BF-{id:N}".ToUpperInvariant();
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        ExternalReference = externalReference;
        Method = method;
        Amount = amount;
        Description = description;
        Status = TransferStatus.PendingProcessing;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public string ExternalReference { get; private set; } = string.Empty;
    public TransferMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public TransferStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? ReversalReason { get; private set; }
    public string? ReversalRequestedBy { get; private set; }
    public DateTimeOffset? ReversalRequestedAtUtc { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Transfer Create(Guid id, Guid sourceAccountId, Guid destinationAccountId,
        string externalReference, TransferMethod method, decimal amount, string description,
        string createdBy, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || sourceAccountId == Guid.Empty || destinationAccountId == Guid.Empty)
        {
            throw new ArgumentException("Os identificadores da transferência são obrigatórios.");
        }

        if (sourceAccountId == destinationAccountId)
        {
            throw new ArgumentException("As contas de origem e destino devem ser diferentes.");
        }

        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), "O método da transferência é inválido.");
        }

        if (amount is < 0.01m or > 50_000_000m || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "O valor deve ficar entre R$ 0,01 e R$ 50.000.000,00, com no máximo duas casas decimais.");
        }

        return new Transfer(id, sourceAccountId, destinationAccountId,
            NormalizeExternalReference(externalReference), method, amount,
            Normalize(description, 300, "descrição"), Normalize(createdBy, 160, "responsável"), createdAtUtc);
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureStatus(TransferStatus.PendingProcessing);
        Status = TransferStatus.Completed;
        StatusReason = null;
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
    }

    public void Reject(string detail)
    {
        EnsureStatus(TransferStatus.PendingProcessing);
        Status = TransferStatus.Rejected;
        StatusReason = Normalize(detail, 500, "motivo da rejeição");
    }

    public void RequestReversal(string reason, string requestedBy, DateTimeOffset requestedAtUtc)
    {
        if (Status is not (TransferStatus.Completed or TransferStatus.ReversalRejected))
        {
            throw new InvalidOperationException("Somente uma transferência concluída pode ter estorno solicitado.");
        }

        Status = TransferStatus.ReversalPending;
        StatusReason = "Estorno em processamento.";
        ReversalReason = Normalize(reason, 300, "motivo do estorno");
        ReversalRequestedBy = Normalize(requestedBy, 160, "responsável");
        ReversalRequestedAtUtc = requestedAtUtc.ToUniversalTime();
    }

    public void CompleteReversal(DateTimeOffset reversedAtUtc)
    {
        EnsureStatus(TransferStatus.ReversalPending);
        Status = TransferStatus.Reversed;
        StatusReason = null;
        ReversedAtUtc = reversedAtUtc.ToUniversalTime();
    }

    public void RejectReversal(string detail)
    {
        EnsureStatus(TransferStatus.ReversalPending);
        Status = TransferStatus.ReversalRejected;
        StatusReason = Normalize(detail, 500, "motivo da rejeição do estorno");
    }

    public static string NormalizeExternalReference(string value) =>
        Normalize(value, 100, "referência externa").ToUpperInvariant();

    private void EnsureStatus(TransferStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"A transferência não aceita esta transição no estado {Status}.");
        }
    }

    private static string Normalize(string value, int maximumLength, string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length < 3 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"A informação de {fieldName} deve possuir entre 3 e {maximumLength} caracteres.", nameof(value));
        }

        return normalized;
    }
}
