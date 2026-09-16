import { accountsGatewayRequest, forbiddenOriginResponse, isSameOrigin } from "@/lib/bff";

export async function POST(request: Request, context: RouteContext<"/api/accounts/[accountId]/cash-in">) {
  if (!isSameOrigin(request)) return forbiddenOriginResponse();
  const { accountId } = await context.params;
  return accountsGatewayRequest(`/api/v1/accounts/${accountId}/cash-in`, { method: "POST", body: await request.text() });
}
