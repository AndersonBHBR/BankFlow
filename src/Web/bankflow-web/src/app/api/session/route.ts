import { NextResponse } from "next/server";
import { sessionCookieName } from "@/lib/auth";
import { gatewayEndpoint, readProblem } from "@/lib/bankflow-api";
import type { TokenResponse } from "@/lib/contracts";

type LoginPayload = {
  login?: unknown;
  password?: unknown;
};

function firstHeaderValue(value: string | null): string | null {
  return value?.split(",")[0]?.trim() || null;
}

function isSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (!origin) {
    return true;
  }

  try {
    const originUrl = new URL(origin);
    const requestUrl = new URL(request.url);
    const requestHost =
      firstHeaderValue(request.headers.get("x-forwarded-host")) ??
      firstHeaderValue(request.headers.get("host")) ??
      requestUrl.host;
    const forwardedProtocol = firstHeaderValue(
      request.headers.get("x-forwarded-proto"),
    );
    const requestProtocol = forwardedProtocol
      ? `${forwardedProtocol}:`
      : requestUrl.protocol;

    return (
      originUrl.host === requestHost && originUrl.protocol === requestProtocol
    );
  } catch {
    return false;
  }
}

function shouldUseSecureCookie(): boolean {
  if (process.env.BANKFLOW_COOKIE_SECURE !== undefined) {
    return process.env.BANKFLOW_COOKIE_SECURE.toLowerCase() === "true";
  }

  return process.env.NODE_ENV === "production";
}

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json(
      { message: "Origem da solicitação não permitida." },
      { status: 403 },
    );
  }

  let payload: LoginPayload;

  try {
    payload = (await request.json()) as LoginPayload;
  } catch {
    return NextResponse.json(
      { message: "A solicitação de acesso é inválida." },
      { status: 400 },
    );
  }

  if (typeof payload.login !== "string" || typeof payload.password !== "string") {
    return NextResponse.json(
      { message: "Informe o login e a senha." },
      { status: 400 },
    );
  }

  try {
    const identityResponse = await fetch(gatewayEndpoint("/api/v1/auth/token"), {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        login: payload.login.trim(),
        password: payload.password,
      }),
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });

    if (!identityResponse.ok) {
      const problem = await readProblem(identityResponse);
      const message =
        identityResponse.status === 401
          ? "Login ou senha inválidos."
          : problem.detail ?? problem.title ?? "Não foi possível autenticar.";

      return NextResponse.json({ message }, { status: identityResponse.status });
    }

    const token = (await identityResponse.json()) as TokenResponse;
    const response = NextResponse.json({ authenticated: true });
    response.cookies.set({
      name: sessionCookieName,
      value: token.accessToken,
      httpOnly: true,
      secure: shouldUseSecureCookie(),
      sameSite: "lax",
      path: "/",
      maxAge: token.expiresIn,
    });

    return response;
  } catch {
    return NextResponse.json(
      {
        message:
          "O Gateway não respondeu. Confirme se o ambiente BankFlow está ativo.",
      },
      { status: 503 },
    );
  }
}

export async function DELETE(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json(
      { message: "Origem da solicitação não permitida." },
      { status: 403 },
    );
  }

  const response = NextResponse.json({ authenticated: false });
  response.cookies.set({
    name: sessionCookieName,
    value: "",
    httpOnly: true,
    secure: shouldUseSecureCookie(),
    sameSite: "lax",
    path: "/",
    maxAge: 0,
  });

  return response;
}
