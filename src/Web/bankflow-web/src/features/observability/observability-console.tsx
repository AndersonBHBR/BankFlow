"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ActivityIcon,
  DatabaseIcon,
  ExternalLinkIcon,
  QueueIcon,
  RefreshIcon,
} from "@/components/icons";
import type {
  ApiProblem,
  ObservabilitySnapshot,
  ServiceObservation,
} from "@/lib/contracts";

type ObservabilityConsoleProps = {
  initialSnapshot: ObservabilitySnapshot;
  links: {
    aspireDashboard: string;
    rabbitMqManagement: string;
  };
};

const stateCopy: Record<
  ServiceObservation["state"],
  { label: string; description: string }
> = {
  operational: {
    label: "Operacional",
    description: "Processo ativo e dependências prontas.",
  },
  degraded: {
    label: "Degradado",
    description: "Processo ativo, mas alguma dependência não está pronta.",
  },
  unavailable: {
    label: "Indisponível",
    description: "O processo não respondeu ao health check.",
  },
};

const sloObjectives = [
  {
    name: "Disponibilidade do Gateway",
    target: "99,9%",
    window: "janela de 30 dias",
  },
  {
    name: "Latência de leitura",
    target: "p95 < 500 ms",
    window: "janela de 30 minutos",
  },
  {
    name: "Processamento assíncrono",
    target: "p95 < 30 s",
    window: "entre Outbox e Inbox",
  },
  {
    name: "Dead-letter queue",
    target: "0 nova",
    window: "sem investigação por 15 min",
  },
] as const;

const diagnosticGuides = [
  {
    title: "Gateway com 502 ou 503",
    check: "Confirme os contêineres e a prontidão do serviço de destino.",
    command: "docker compose ps",
  },
  {
    title: "Transferências ou Contas degradado",
    check: "Verifique SQL Server e a conexão autenticada com o RabbitMQ.",
    command: "docker compose logs transfers accounts",
  },
  {
    title: "Transferência em processamento",
    check: "Inspecione Outbox, filas, consumidores e DLQs preservando o MessageId.",
    command: "docker compose logs rabbitmq transfers accounts",
  },
  {
    title: "Trace ausente no Aspire",
    check: "Valide o exportador OTLP e a porta 18889 na rede Compose.",
    command: "docker compose logs aspire-dashboard",
  },
] as const;

function formatTimestamp(value: string): string {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "medium",
    timeStyle: "medium",
    timeZone: "America/Sao_Paulo",
  }).format(new Date(value));
}

function endpointUrl(service: ServiceObservation, endpoint: string): string {
  return `${service.endpointUrl}/${endpoint}`;
}

function probeCopy(
  status: "operational" | "unavailable",
  latencyMs: number | null,
): string {
  if (status === "unavailable") {
    return "Falhou";
  }

  return latencyMs === null ? "Respondeu" : `${latencyMs} ms`;
}

export function ObservabilityConsole({
  initialSnapshot,
  links,
}: ObservabilityConsoleProps) {
  const router = useRouter();
  const [snapshot, setSnapshot] = useState(initialSnapshot);
  const [refreshing, setRefreshing] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refreshSnapshot = useCallback(async () => {
    setRefreshing(true);

    try {
      const response = await fetch("/api/observability/health", {
        cache: "no-store",
      });

      if (response.status === 401) {
        router.replace("/login");
        return;
      }

      if (!response.ok) {
        const problem = (await response.json()) as ApiProblem;
        throw new Error(
          problem.detail ?? "Não foi possível atualizar a saúde da plataforma.",
        );
      }

      setSnapshot((await response.json()) as ObservabilitySnapshot);
      setError(null);
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Falha inesperada durante a atualização.",
      );
    } finally {
      setRefreshing(false);
    }
  }, [router]);

  useEffect(() => {
    if (!autoRefresh) {
      return;
    }

    const interval = window.setInterval(() => {
      void refreshSnapshot();
    }, 10_000);

    const refreshWhenVisible = () => {
      if (document.visibilityState === "visible") {
        void refreshSnapshot();
      }
    };

    document.addEventListener("visibilitychange", refreshWhenVisible);

    return () => {
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", refreshWhenVisible);
    };
  }, [autoRefresh, refreshSnapshot]);

  const operationalCount = snapshot.services.filter(
    (service) => service.state === "operational",
  ).length;
  const readyCount = snapshot.services.filter(
    (service) => service.readiness.status === "operational",
  ).length;
  const liveCount = snapshot.services.filter(
    (service) => service.liveness.status === "operational",
  ).length;
  const affectedServices = snapshot.services.filter(
    (service) => service.state !== "operational",
  );
  const readinessLatencies = snapshot.services
    .map((service) => service.readiness.latencyMs)
    .filter((latency): latency is number => latency !== null);
  const averageLatency =
    readinessLatencies.length === 0
      ? null
      : Math.round(
          readinessLatencies.reduce((total, latency) => total + latency, 0) /
            readinessLatencies.length,
        );
  const platformState =
    operationalCount === snapshot.services.length
      ? "operational"
      : liveCount === snapshot.services.length
        ? "degraded"
        : "unavailable";

  return (
    <main className="observability-page">
      <header className="page-heading observability-heading">
        <div>
          <span className="eyebrow">Plataforma e confiabilidade</span>
          <h1>Observabilidade</h1>
          <p>Liveness, readiness e diagnóstico rápido do BankFlow.</p>
        </div>

        <div className="observability-actions">
          <button
            type="button"
            className={`auto-refresh-control ${autoRefresh ? "is-active" : ""}`}
            onClick={() => setAutoRefresh((current) => !current)}
            aria-pressed={autoRefresh}
          >
            <span />
            Automático · 10 s
          </button>
          <button
            type="button"
            className="observability-refresh-button"
            onClick={() => void refreshSnapshot()}
            disabled={refreshing}
          >
            <RefreshIcon className={refreshing ? "is-spinning" : ""} />
            {refreshing ? "Atualizando" : "Atualizar agora"}
          </button>
        </div>
      </header>

      {error ? (
        <div className="observability-message is-error" role="alert">
          <strong>Atualização interrompida</strong>
          <span>{error}</span>
        </div>
      ) : null}

      <section
        className={`platform-summary is-${platformState}`}
        aria-live="polite"
      >
        <div className="platform-summary-state">
          <span className="platform-pulse" />
          <div>
            <small>Estado consolidado</small>
            <strong>{stateCopy[platformState].label}</strong>
          </div>
        </div>
        <p>{stateCopy[platformState].description}</p>
        <span className="platform-summary-time">
          Atualizado em {formatTimestamp(snapshot.checkedAtUtc)}
        </span>
      </section>

      <section className="observability-metrics" aria-label="Resumo operacional">
        <article>
          <span>Serviços operacionais</span>
          <strong>
            {operationalCount}/{snapshot.services.length}
          </strong>
          <p>Liveness e readiness aprovados</p>
        </article>
        <article>
          <span>Processos ativos</span>
          <strong>
            {liveCount}/{snapshot.services.length}
          </strong>
          <p>Resposta do processo da aplicação</p>
        </article>
        <article>
          <span>Serviços prontos</span>
          <strong>
            {readyCount}/{snapshot.services.length}
          </strong>
          <p>Dependências disponíveis</p>
        </article>
        <article>
          <span>Latência média atual</span>
          <strong>{averageLatency === null ? "—" : `${averageLatency} ms`}</strong>
          <p>Amostra instantânea de readiness</p>
        </article>
      </section>

      <section className="dashboard-section">
        <div className="section-heading">
          <div>
            <span className="section-index">01</span>
            <h2>Mapa de saúde</h2>
          </div>
          <span className="section-note">liveness + readiness</span>
        </div>

        <div className="observability-service-grid">
          {snapshot.services.map((service) => (
            <article
              className={`observability-service-card is-${service.state}`}
              key={service.key}
            >
              <header>
                <span className="observability-service-icon">
                  <ActivityIcon />
                </span>
                <div>
                  <span className="observability-state-label">
                    {stateCopy[service.state].label}
                  </span>
                  <h3>{service.name}</h3>
                </div>
              </header>

              <p>{service.description}</p>

              <dl className="probe-list">
                <div>
                  <dt>Liveness</dt>
                  <dd className={`is-${service.liveness.status}`}>
                    {probeCopy(
                      service.liveness.status,
                      service.liveness.latencyMs,
                    )}
                  </dd>
                </div>
                <div>
                  <dt>Readiness</dt>
                  <dd className={`is-${service.readiness.status}`}>
                    {probeCopy(
                      service.readiness.status,
                      service.readiness.latencyMs,
                    )}
                  </dd>
                </div>
              </dl>

              {service.endpointUrl ? (
                <a
                  href={endpointUrl(service, "health/ready")}
                  target="_blank"
                  rel="noreferrer"
                  className="service-endpoint-link"
                >
                  Abrir endpoint <ExternalLinkIcon />
                </a>
              ) : (
                <span className="service-endpoint-link is-restricted">
                  Acesso restrito à rede interna
                </span>
              )}
            </article>
          ))}
        </div>
      </section>

      <section className="dashboard-section">
        <div className="section-heading">
          <div>
            <span className="section-index">02</span>
            <h2>Objetivos de confiabilidade</h2>
          </div>
          <span className="section-note">SLOs de referência</span>
        </div>

        <div className="slo-grid">
          {sloObjectives.map((objective) => (
            <article key={objective.name}>
              <span>Objetivo</span>
              <h3>{objective.name}</h3>
              <strong>{objective.target}</strong>
              <p>{objective.window}</p>
            </article>
          ))}
        </div>
        <p className="measurement-disclaimer">
          Estes valores são metas arquiteturais. Percentis, séries históricas e
          traces reais devem ser analisados no Aspire Dashboard.
        </p>
      </section>

      <section className="dashboard-section">
        <div className="section-heading">
          <div>
            <span className="section-index">03</span>
            <h2>Ferramentas operacionais</h2>
          </div>
          <span className="section-note">ambiente local</span>
        </div>

        <div className="operations-tool-grid">
          <a href={links.aspireDashboard} target="_blank" rel="noreferrer">
            <span className="operations-tool-icon">
              <ActivityIcon />
            </span>
            <div>
              <small>Telemetria distribuída</small>
              <h3>Aspire Dashboard</h3>
              <p>Traces, métricas, logs e dependências dos serviços.</p>
            </div>
            <ExternalLinkIcon />
          </a>

          <a href={links.rabbitMqManagement} target="_blank" rel="noreferrer">
            <span className="operations-tool-icon is-orange">
              <QueueIcon />
            </span>
            <div>
              <small>Mensageria</small>
              <h3>RabbitMQ Management</h3>
              <p>Filas, consumidores, exchanges e dead-letter queues.</p>
            </div>
            <ExternalLinkIcon />
          </a>

          <article>
            <span className="operations-tool-icon is-blue">
              <DatabaseIcon />
            </span>
            <div>
              <small>Teste controlado</small>
              <h3>Ensaio de carga</h3>
              <p>Execute no terminal para validar thresholds do Grafana k6.</p>
              <code>.\scripts\run-load-test.ps1</code>
            </div>
          </article>
        </div>
      </section>

      <section className="dashboard-section observability-runbook">
        <div className="section-heading">
          <div>
            <span className="section-index">04</span>
            <h2>Diagnóstico guiado</h2>
          </div>
          <span className="section-note">ações seguras do runbook</span>
        </div>

        <div
          className={`incident-summary ${
            affectedServices.length === 0 ? "is-clear" : "is-attention"
          }`}
        >
          <strong>
            {affectedServices.length === 0
              ? "Nenhuma indisponibilidade detectada"
              : `${affectedServices.length} serviço(s) requer(em) atenção`}
          </strong>
          <span>
            {affectedServices.length === 0
              ? "Todos os processos e suas dependências responderam nesta coleta."
              : affectedServices
                  .map(
                    (service) =>
                      `${service.name}: ${stateCopy[service.state].description}`,
                  )
                  .join(" · ")}
          </span>
        </div>

        <div className="diagnostic-grid">
          {diagnosticGuides.map((guide, index) => (
            <article key={guide.title}>
              <span>{String(index + 1).padStart(2, "0")}</span>
              <h3>{guide.title}</h3>
              <p>{guide.check}</p>
              <code>{guide.command}</code>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}
