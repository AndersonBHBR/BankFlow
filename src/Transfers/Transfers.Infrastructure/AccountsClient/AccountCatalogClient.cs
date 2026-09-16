using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Transfers.Application.Abstractions;

namespace Transfers.Infrastructure.Accounts;

public sealed class AccountCatalogClient(HttpClient httpClient) : IAccountCatalog
{
    public async Task<AccountLookupResult> GetAccountAsync(
        Guid accountId, string authorization, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/accounts/{accountId}");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new AccountLookupResult(AccountLookupStatus.NotFound, null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Unavailable($"O serviço de Contas respondeu com HTTP {(int)response.StatusCode}.");
            }

            var account = await response.Content.ReadFromJsonAsync<AccountSnapshot>(cancellationToken: cancellationToken);
            return account is not null && account.Id == accountId
                ? new AccountLookupResult(AccountLookupStatus.Found, account, null)
                : Unavailable("O serviço de Contas devolveu uma resposta inválida.");
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(exception.Message);
        }
        catch (JsonException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Unavailable(exception.Message);
        }
    }

    private static AccountLookupResult Unavailable(string detail) =>
        new(AccountLookupStatus.Unavailable, null, $"Falha ao consultar Contas: {detail}");
}
