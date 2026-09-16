using Accounts.Application.Abstractions;
using Accounts.Application.Common;
using Accounts.Domain.Accounts;
using Accounts.Domain.Ledger;

namespace Accounts.Application.Accounts;

public sealed class AccountService(IAccountRepository repository, TimeProvider timeProvider)
{
    public async Task<AccountsResult<AccountResponse>> CreateAsync(
        CreateAccountCommand command, CancellationToken cancellationToken)
    {
        BankAccount account;
        try
        {
            account = BankAccount.Create(command.Number, command.HolderId, command.HolderName,
                command.Document, command.PixKey, command.Type, command.InitialBalance,
                command.DailyTransferLimit, command.NightlyTransferLimit, timeProvider.GetUtcNow());
            ArgumentException.ThrowIfNullOrWhiteSpace(command.PerformedBy);
        }
        catch (ArgumentException exception)
        {
            return Validation<AccountResponse>(exception.Message);
        }

        if (await repository.NumberOrPixKeyExistsAsync(account.Number, account.PixKey, cancellationToken))
        {
            return Duplicate<AccountResponse>();
        }

        repository.Add(account);
        if (account.Balance > 0)
        {
            repository.AddLedgerEntry(LedgerEntry.Create(account.Id, null, LedgerEntryType.CashIn,
                account.Balance, account.Balance, "Aporte inicial de demonstração.",
                command.PerformedBy, timeProvider.GetUtcNow()));
        }

        return await repository.SaveChangesAsync(cancellationToken) switch
        {
            AccountSaveOutcome.Success => AccountsResult.Success(AccountResponse.From(account)),
            AccountSaveOutcome.DuplicateResource => Duplicate<AccountResponse>(),
            _ => Concurrency<AccountResponse>()
        };
    }

    public async Task<AccountsResult<AccountResponse>> UpdateAsync(
        UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        var version = DecodeRowVersion(command.RowVersion);
        if (!version.IsSuccess)
        {
            return AccountsResult.Failure<AccountResponse>(version.Error!.Code, version.Error.Message);
        }

        var account = await repository.GetByIdAsync(command.AccountId, true, cancellationToken);
        if (account is null)
        {
            return NotFound<AccountResponse>();
        }

        try
        {
            account.UpdateLimitsAndStatus(command.DailyTransferLimit, command.NightlyTransferLimit, command.Status);
        }
        catch (ArgumentException exception)
        {
            return Validation<AccountResponse>(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict<AccountResponse>(exception.Message);
        }

        repository.SetExpectedRowVersion(account, version.Value!);
        return await repository.SaveChangesAsync(cancellationToken) == AccountSaveOutcome.Success
            ? AccountsResult.Success(AccountResponse.From(account))
            : Concurrency<AccountResponse>();
    }

    public async Task<AccountsResult<CashInResponse>> CashInAsync(
        CashInCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.AccountId, true, cancellationToken);
        if (account is null)
        {
            return NotFound<CashInResponse>();
        }

        LedgerEntry entry;
        try
        {
            var balance = account.Credit(command.Amount);
            entry = LedgerEntry.Create(account.Id, null, LedgerEntryType.CashIn, command.Amount,
                balance, command.Description, command.PerformedBy, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation<CashInResponse>(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict<CashInResponse>(exception.Message);
        }

        repository.AddLedgerEntry(entry);
        return await repository.SaveChangesAsync(cancellationToken) == AccountSaveOutcome.Success
            ? AccountsResult.Success(new CashInResponse(AccountResponse.From(account), LedgerEntryResponse.From(entry)))
            : Concurrency<CashInResponse>();
    }

    public async Task<AccountsResult<AccountResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(id, false, cancellationToken);
        return account is null ? NotFound<AccountResponse>() : AccountsResult.Success(AccountResponse.From(account));
    }

    public async Task<AccountsResult<AccountResponse>> GetByPixKeyAsync(string pixKey, CancellationToken cancellationToken)
    {
        string normalized;
        try
        {
            normalized = BankAccount.NormalizePixKey(pixKey);
        }
        catch (ArgumentException exception)
        {
            return Validation<AccountResponse>(exception.Message);
        }

        var account = await repository.GetByPixKeyAsync(normalized, cancellationToken);
        return account is null ? NotFound<AccountResponse>() : AccountsResult.Success(AccountResponse.From(account));
    }

    public async Task<AccountsResult<PagedResult<AccountResponse>>> ListAsync(
        int page, int pageSize, string? search, AccountStatus? status, CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(page, pageSize);
        if (paginationError is not null)
        {
            return Validation<PagedResult<AccountResponse>>(paginationError);
        }

        if (status is not null && !Enum.IsDefined(status.Value))
        {
            return Validation<PagedResult<AccountResponse>>("A situação informada é inválida.");
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch?.Length > 100)
        {
            return Validation<PagedResult<AccountResponse>>("A busca deve possuir no máximo 100 caracteres.");
        }

        var (items, count) = await repository.ListAsync(page, pageSize, normalizedSearch, status, cancellationToken);
        return AccountsResult.Success(new PagedResult<AccountResponse>(
            items.Select(AccountResponse.From).ToArray(), page, pageSize, count));
    }

    public async Task<AccountsResult<PagedResult<LedgerEntryResponse>>> ListLedgerAsync(
        Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(page, pageSize);
        if (paginationError is not null)
        {
            return Validation<PagedResult<LedgerEntryResponse>>(paginationError);
        }

        if (await repository.GetByIdAsync(accountId, false, cancellationToken) is null)
        {
            return NotFound<PagedResult<LedgerEntryResponse>>();
        }

        var (items, count) = await repository.ListLedgerAsync(accountId, page, pageSize, cancellationToken);
        return AccountsResult.Success(new PagedResult<LedgerEntryResponse>(
            items.Select(LedgerEntryResponse.From).ToArray(), page, pageSize, count));
    }

    private static AccountsResult<byte[]> DecodeRowVersion(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value ?? string.Empty);
            return bytes.Length == 8 ? AccountsResult.Success(bytes) : Validation<byte[]>("A versão da conta é inválida.");
        }
        catch (FormatException)
        {
            return Validation<byte[]>("A versão da conta é inválida.");
        }
    }

    private static string? ValidatePagination(int page, int pageSize) => page < 1
        ? "A página deve ser maior ou igual a 1."
        : pageSize is < 1 or > 100 ? "O tamanho da página deve ficar entre 1 e 100." : null;

    private static AccountsResult<T> Validation<T>(string message) => AccountsResult.Failure<T>(AccountsErrorCode.Validation, message);
    private static AccountsResult<T> NotFound<T>() => AccountsResult.Failure<T>(AccountsErrorCode.NotFound, "Conta não encontrada.");
    private static AccountsResult<T> Duplicate<T>() => AccountsResult.Failure<T>(AccountsErrorCode.DuplicateResource, "O número da conta ou a chave Pix já está em uso.");
    private static AccountsResult<T> Conflict<T>(string message) => AccountsResult.Failure<T>(AccountsErrorCode.Conflict, message);
    private static AccountsResult<T> Concurrency<T>() => AccountsResult.Failure<T>(AccountsErrorCode.ConcurrencyConflict, "A conta foi alterada durante a operação. Consulte novamente e repita a ação.");
}
