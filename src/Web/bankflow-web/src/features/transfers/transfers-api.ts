import type { ApiProblem, PagedResult, Transfer, TransferMethod, TransferStatus } from "@/lib/contracts";

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

export function listTransfers(status: TransferStatus | "" = ""): Promise<PagedResult<Transfer>> {
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  if (status) params.set("status", status);
  return request(`/api/transfers?${params}`);
}

export function createTransfer(input: { sourceAccountId: string; destinationAccountId: string; externalReference: string; method: TransferMethod; amount: number; description: string }): Promise<Transfer> {
  return request("/api/transfers", { method: "POST", body: JSON.stringify(input) });
}

export function getTransfer(id: string): Promise<Transfer> {
  return request(`/api/transfers/${id}`);
}

export function requestReversal(transfer: Transfer, reason: string): Promise<Transfer> {
  return request(`/api/transfers/${transfer.id}/reversal`, { method: "POST", body: JSON.stringify({ rowVersion: transfer.rowVersion, reason }) });
}
