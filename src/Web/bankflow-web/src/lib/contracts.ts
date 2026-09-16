export type TokenResponse = { accessToken: string; tokenType: string; expiresIn: number; expiresAtUtc: string };
export type CurrentUser = { subject: string; name: string; roles: string[] };
export type ServiceHealth = { key: "gateway" | "identity" | "transfers" | "accounts"; name: string; description: string; status: "operational" | "unavailable"; latencyMs: number | null };
export type HealthProbe = { status: "operational" | "unavailable"; latencyMs: number | null; httpStatus: number | null };
export type ServiceObservation = { key: ServiceHealth["key"]; name: string; description: string; endpointUrl: string; state: "operational" | "degraded" | "unavailable"; liveness: HealthProbe; readiness: HealthProbe };
export type ObservabilitySnapshot = { services: ServiceObservation[]; checkedAtUtc: string };
export type ApiProblem = { title?: string; detail?: string; status?: number; traceId?: string };
export type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export type AccountType = "Checking" | "Savings";
export type AccountStatus = "Active" | "Blocked" | "Closed";
export type BankAccount = {
  id: string; number: string; holderId: string; holderName: string; document: string;
  pixKey: string; type: AccountType; status: AccountStatus; balance: number;
  dailyTransferLimit: number; nightlyTransferLimit: number; openedAtUtc: string; rowVersion: string;
};
export type LedgerEntryType = "CashIn" | "TransferDebit" | "TransferCredit" | "ReversalDebit" | "ReversalCredit";
export type LedgerEntry = { id: string; accountId: string; transferId: string | null; type: LedgerEntryType; amount: number; balanceAfter: number; description: string; performedBy: string; occurredAtUtc: string };
export type CashInResult = { account: BankAccount; entry: LedgerEntry };

export type TransferMethod = "Pix" | "Internal" | "Ted";
export type TransferStatus = "PendingProcessing" | "Completed" | "Rejected" | "ReversalPending" | "Reversed" | "ReversalRejected";
export type Transfer = {
  id: string; number: string; sourceAccountId: string; destinationAccountId: string;
  externalReference: string; method: TransferMethod; amount: number; description: string;
  status: TransferStatus; statusReason: string | null; createdBy: string; createdAtUtc: string;
  completedAtUtc: string | null; reversalReason: string | null; reversalRequestedBy: string | null;
  reversalRequestedAtUtc: string | null; reversedAtUtc: string | null; rowVersion: string;
};
