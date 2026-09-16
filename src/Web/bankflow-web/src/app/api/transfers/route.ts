import { forbiddenOriginResponse, gatewayBffRequest, isSameOrigin, transferListQuery } from "@/lib/bff";

export async function GET(request: Request) {
  return gatewayBffRequest(`/api/v1/transfers${transferListQuery(request)}`);
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return forbiddenOriginResponse();
  return gatewayBffRequest("/api/v1/transfers", { method: "POST", body: await request.text() });
}
