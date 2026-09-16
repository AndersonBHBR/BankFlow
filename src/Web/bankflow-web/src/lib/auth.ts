import { cookies } from "next/headers";
import type { CurrentUser } from "@/lib/contracts";
import { gatewayFetch } from "@/lib/bankflow-api";

export const sessionCookieName = "bankflow_session";

export async function getSessionToken(): Promise<string | null> {
  const cookieStore = await cookies();
  return cookieStore.get(sessionCookieName)?.value ?? null;
}

export async function getCurrentUser(): Promise<CurrentUser | null> {
  const token = await getSessionToken();
  if (!token) {
    return null;
  }

  try {
    const response = await gatewayFetch("/api/v1/auth/me", token, {
      signal: AbortSignal.timeout(3000),
    });

    if (!response.ok) {
      return null;
    }

    return (await response.json()) as CurrentUser;
  } catch {
    return null;
  }
}
