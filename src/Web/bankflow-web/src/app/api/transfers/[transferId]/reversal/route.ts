import { forbiddenOriginResponse, gatewayBffRequest, isSameOrigin } from "@/lib/bff";

export async function POST(request: Request, context: RouteContext<"/api/transfers/[transferId]/reversal">) {
  if (!isSameOrigin(request)) return forbiddenOriginResponse();
  const { transferId } = await context.params;
  return gatewayBffRequest(`/api/v1/transfers/${transferId}/reversal`, { method: "POST", body: await request.text() });
}
