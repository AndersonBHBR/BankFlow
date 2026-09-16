import { accountsGatewayRequest, forbiddenOriginResponse, isSameOrigin } from "@/lib/bff";

export async function GET(_: Request, context: RouteContext<"/api/accounts/[accountId]">) {
  const { accountId } = await context.params;
  return accountsGatewayRequest(`/api/v1/accounts/${accountId}`);
}

export async function PUT(request: Request, context: RouteContext<"/api/accounts/[accountId]">) {
  if (!isSameOrigin(request)) return forbiddenOriginResponse();
  const { accountId } = await context.params;
  return accountsGatewayRequest(`/api/v1/accounts/${accountId}`, { method: "PUT", body: await request.text() });
}
