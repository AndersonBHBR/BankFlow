import type { ApiProblem, BankAccount, CashInResult, LedgerEntry, PagedResult } from "@/lib/contracts";

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, { ...init, headers: init?.body ? { "Content-Type": "application/json", ...init.headers } : init?.headers });
  if (response.status === 401) {
    throw new Error("Sessão expirada. Entre novamente para continuar.");
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ApiProblem;
    throw new Error(problem.detail ?? "Não foi possível concluir a operação.");
  }
  return response.json() as Promise<T>;
}

export function listAccounts(search = ""): Promise<PagedResult<BankAccount>> {
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  if (search.trim()) params.set("search", search.trim());
  return request(`/api/accounts?${params}`);
}

export function createAccount(input: Omit<BankAccount, "id" | "status" | "openedAtUtc" | "rowVersion" | "balance"> & { initialBalance: number }): Promise<BankAccount> {
  return request("/api/accounts", { method: "POST", body: JSON.stringify(input) });
}

export function updateAccount(account: BankAccount, input: Pick<BankAccount, "dailyTransferLimit" | "nightlyTransferLimit" | "status">): Promise<BankAccount> {
  return request(`/api/accounts/${account.id}`, { method: "PUT", body: JSON.stringify({ ...input, rowVersion: account.rowVersion }) });
}

export function cashIn(accountId: string, amount: number, description: string): Promise<CashInResult> {
  return request(`/api/accounts/${accountId}/cash-in`, { method: "POST", body: JSON.stringify({ amount, description }) });
}

export function listLedger(accountId: string): Promise<PagedResult<LedgerEntry>> {
  return request(`/api/accounts/${accountId}/ledger?page=1&pageSize=100`);
}
