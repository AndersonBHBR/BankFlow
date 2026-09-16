import { gatewayBffRequest } from "@/lib/bff";

export async function GET(_: Request, context: RouteContext<"/api/transfers/[transferId]">) {
  const { transferId } = await context.params;
  return gatewayBffRequest(`/api/v1/transfers/${transferId}`);
}
