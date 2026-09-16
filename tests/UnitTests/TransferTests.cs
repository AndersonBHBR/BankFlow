using Transfers.Domain.Transfers;
using Xunit;

namespace UnitTests;

public sealed class TransferTests
{
    [Fact]
    public void Create_WithValidData_StartsPending()
    {
        var transfer = CreateTransfer();

        Assert.Equal(TransferStatus.PendingProcessing, transfer.Status);
        Assert.Equal(250.50m, transfer.Amount);
        Assert.StartsWith("BF-", transfer.Number);
    }

    [Fact]
    public void Create_ToSameAccount_IsRejected()
    {
        var accountId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => Transfer.Create(Guid.NewGuid(), accountId,
            accountId, "PIX-001", TransferMethod.Pix, 10m, "Pagamento teste",
            "operator", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompletedTransfer_CanBeReversed()
    {
        var transfer = CreateTransfer();
        transfer.Complete(DateTimeOffset.UtcNow);
        transfer.RequestReversal("Transferência duplicada", "operator", DateTimeOffset.UtcNow);
        transfer.CompleteReversal(DateTimeOffset.UtcNow);

        Assert.Equal(TransferStatus.Reversed, transfer.Status);
        Assert.NotNull(transfer.ReversedAtUtc);
    }

    [Fact]
    public void RejectedTransfer_CannotBeReversed()
    {
        var transfer = CreateTransfer();
        transfer.Reject("Saldo disponível insuficiente.");

        Assert.Throws<InvalidOperationException>(() =>
            transfer.RequestReversal("Solicitação do cliente", "operator", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void TransferAmount_RejectsMoreThanTwoDecimals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransfer(1.001m));
    }

    private static Transfer CreateTransfer(decimal amount = 250.50m) => Transfer.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "pix-demo-001", TransferMethod.Pix,
        amount, "Pagamento de demonstração", "operator", DateTimeOffset.UtcNow);
}
