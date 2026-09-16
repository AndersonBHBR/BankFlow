import type {
  ApiProblem,
  HealthProbe,
  ObservabilitySnapshot,
  ServiceHealth,
  ServiceObservation,
} from "@/lib/contracts";

const gatewayUrl = process.env.BANKFLOW_GATEWAY_URL ?? "http://localhost:8080";
const localPublicUrl = (port: number) =>
  process.env.NODE_ENV === "production" ? "" : `http://localhost:${port}`;

const serviceDefinitions = [
  {
    key: "gateway",
    name: "API Gateway",
    description: "Entrada única, autorização e rate limiting",
    url: gatewayUrl,
    publicUrl:
      process.env.BANKFLOW_GATEWAY_PUBLIC_URL ?? localPublicUrl(8080),
  },
  {
    key: "identity",
    name: "Identity",
    description: "Emissão e validação de credenciais JWT",
    url: process.env.BANKFLOW_IDENTITY_URL ?? "http://localhost:8081",
    publicUrl:
      process.env.BANKFLOW_IDENTITY_PUBLIC_URL ?? localPublicUrl(8081),
  },
  {
    key: "transfers",
    name: "Transferências",
    description: "Pix, idempotência, liquidação e estornos",
    url: process.env.BANKFLOW_TRANSFERS_URL ?? "http://localhost:8082",
    publicUrl: process.env.BANKFLOW_TRANSFERS_PUBLIC_URL ?? localPublicUrl(8082),
  },
  {
    key: "accounts",
    name: "Contas",
    description: "Saldos, limites e razão contábil imutável",
    url: process.env.BANKFLOW_ACCOUNTS_URL ?? "http://localhost:8083",
    publicUrl:
      process.env.BANKFLOW_ACCOUNTS_PUBLIC_URL ?? localPublicUrl(8083),
  },
] as const;

export function gatewayEndpoint(path: string): string {
  const normalizedBase = gatewayUrl.endsWith("/") ? gatewayUrl : `${gatewayUrl}/`;
  return new URL(path.replace(/^\//, ""), normalizedBase).toString();
}

export async function gatewayFetch(
  path: string,
  token: string,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  headers.set("Authorization", `Bearer ${token}`);

  return fetch(gatewayEndpoint(path), {
    ...init,
    headers,
    cache: "no-store",
  });
}

export async function readProblem(response: Response): Promise<ApiProblem> {
  try {
    return (await response.json()) as ApiProblem;
  } catch {
    return {
      status: response.status,
      title: "Falha de comunicação",
      detail: "O serviço não retornou uma resposta no formato esperado.",
    };
  }
}

async function checkService(
  definition: (typeof serviceDefinitions)[number],
): Promise<ServiceHealth> {
  const startedAt = performance.now();

  try {
    const baseUrl = definition.url.endsWith("/")
      ? definition.url
      : `${definition.url}/`;
    const response = await fetch(new URL("health/live", baseUrl), {
      cache: "no-store",
      signal: AbortSignal.timeout(2500),
    });

    return {
      key: definition.key,
      name: definition.name,
      description: definition.description,
      status: response.ok ? "operational" : "unavailable",
      latencyMs: Math.max(1, Math.round(performance.now() - startedAt)),
    };
  } catch {
    return {
      key: definition.key,
      name: definition.name,
      description: definition.description,
      status: "unavailable",
      latencyMs: null,
    };
  }
}

export async function getPlatformHealth(): Promise<ServiceHealth[]> {
  return Promise.all(serviceDefinitions.map(checkService));
}

async function checkHealthEndpoint(
  baseUrl: string,
  endpoint: "health/live" | "health/ready",
): Promise<HealthProbe> {
  const startedAt = performance.now();

  try {
    const normalizedBase = baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`;
    const response = await fetch(new URL(endpoint, normalizedBase), {
      cache: "no-store",
      signal: AbortSignal.timeout(2500),
    });

    return {
      status: response.ok ? "operational" : "unavailable",
      latencyMs: Math.max(1, Math.round(performance.now() - startedAt)),
      httpStatus: response.status,
    };
  } catch {
    return {
      status: "unavailable",
      latencyMs: null,
      httpStatus: null,
    };
  }
}

async function observeService(
  definition: (typeof serviceDefinitions)[number],
): Promise<ServiceObservation> {
  const [liveness, readiness] = await Promise.all([
    checkHealthEndpoint(definition.url, "health/live"),
    checkHealthEndpoint(definition.url, "health/ready"),
  ]);

  const state =
    liveness.status === "unavailable"
      ? "unavailable"
      : readiness.status === "unavailable"
        ? "degraded"
        : "operational";

  return {
    key: definition.key,
    name: definition.name,
    description: definition.description,
    endpointUrl: definition.publicUrl.replace(/\/$/, ""),
    state,
    liveness,
    readiness,
  };
}

export async function getPlatformObservability(): Promise<ObservabilitySnapshot> {
  const services = await Promise.all(serviceDefinitions.map(observeService));

  return {
    services,
    checkedAtUtc: new Date().toISOString(),
  };
}
