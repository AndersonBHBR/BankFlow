import { NextResponse } from "next/server";
import { getSessionToken } from "@/lib/auth";
import { getPlatformObservability } from "@/lib/bankflow-api";

export const dynamic = "force-dynamic";

export async function GET() {
  const token = await getSessionToken();
  if (!token) {
    return NextResponse.json(
      { title: "Sessão expirada", detail: "Entre novamente para continuar." },
      { status: 401 },
    );
  }

  const snapshot = await getPlatformObservability();

  return NextResponse.json(snapshot, {
    headers: {
      "Cache-Control": "no-store, max-age=0",
    },
  });
}
