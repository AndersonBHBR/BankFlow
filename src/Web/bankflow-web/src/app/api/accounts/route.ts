import { accountsGatewayRequest, accountsListQuery, forbiddenOriginResponse, isSameOrigin } from "@/lib/bff";

export async function GET(request: Request) {
  return accountsGatewayRequest(`/api/v1/accounts${accountsListQuery(request)}`);
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOriginResponse();
  return accountsGatewayRequest("/api/v1/accounts", { method: "POST", body: await request.text() });
}
