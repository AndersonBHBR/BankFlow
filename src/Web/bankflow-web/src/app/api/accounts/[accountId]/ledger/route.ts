import { accountsGatewayRequest, ledgerListQuery } from "@/lib/bff";

export async function GET(request: Request, context: RouteContext<"/api/accounts/[accountId]/ledger">) {
  const { accountId } = await context.params;
  return accountsGatewayRequest(`/api/v1/accounts/${accountId}/ledger${ledgerListQuery(request)}`);
}
