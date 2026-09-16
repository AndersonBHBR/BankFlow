import Link from "next/link";
import {
  ActivityIcon,
  ArrowIcon,
  BoxIcon,
  OrdersIcon,
} from "@/components/icons";
import { ServiceStatusCard } from "@/components/service-status-card";
import { getPlatformHealth } from "@/lib/bankflow-api";

function formatTimestamp(date: Date) {
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "America/Sao_Paulo",
  }).format(date);
}

export default async function DashboardPage() {
  const services = await getPlatformHealth();
  const operationalCount = services.filter(
    (service) => service.status === "operational",
  ).length;
  const platformOperational = operationalCount === services.length;

  return (
    <main className="dashboard-page">
      <header className="page-heading">
        <div>
          <span className="eyebrow">Digital banking lab</span>
          <h1>Operação financeira</h1>
          <p>Contas, transferências e confiabilidade em uma visão única.</p>
        </div>
        <div className={`global-status ${platformOperational ? "is-up" : "is-down"}`}>
          <span />
          <div>
            <strong>
              {platformOperational ? "Plataforma operacional" : "Atenção necessária"}
            </strong>
            <small>
              {operationalCount}/{services.length} serviços disponíveis
            </small>
          </div>
        </div>
      </header>

      <section className="metric-grid" aria-label="Indicadores principais">
        <article className="metric-card metric-card-highlight">
          <span className="metric-label">Disponibilidade</span>
          <strong>
            {services.length === 0
              ? "0%"
              : `${Math.round((operationalCount / services.length) * 100)}%`}
          </strong>
          <p>Leitura atual dos health checks</p>
        </article>
        <article className="metric-card">
          <span className="metric-label">Serviços monitorados</span>
          <strong>{services.length}</strong>
          <p>Gateway, identidade e domínios bancários</p>
        </article>
        <article className="metric-card">
          <span className="metric-label">Atualizado em</span>
          <strong className="metric-time">{formatTimestamp(new Date())}</strong>
          <p>Dados sem cache, coletados nesta requisição</p>
        </article>
      </section>

      <section className="dashboard-section">
        <div className="section-heading">
          <div>
            <span className="section-index">01</span>
            <h2>Saúde dos serviços</h2>
          </div>
          <span className="live-indicator">
            <i /> ao vivo
          </span>
        </div>

        <div className="service-grid">
          {services.map((service) => (
            <ServiceStatusCard key={service.key} service={service} />
          ))}
        </div>
      </section>

      <section className="dashboard-section">
        <div className="section-heading">
          <div>
            <span className="section-index">02</span>
            <h2>Módulos operacionais</h2>
          </div>
          <span className="section-note">acessos por domínio</span>
        </div>

        <div className="module-grid">
          <article className="module-card">
            <span className="module-icon">
              <BoxIcon />
            </span>
            <div>
              <span className="module-state">Integrado ao Gateway</span>
              <h3>Contas e razão</h3>
              <p>
                Onboarding, saldo, limites, bloqueio e lançamentos imutáveis.
              </p>
            </div>
            <Link className="module-action" href="/contas">
              Abrir módulo <ArrowIcon />
            </Link>
          </article>

          <article className="module-card">
            <span className="module-icon module-icon-orange">
              <OrdersIcon />
            </span>
            <div>
              <span className="module-state">Integrado à mensageria</span>
              <h3>Pix e transferências</h3>
              <p>
                Idempotência, liquidação assíncrona, rejeições e estornos.
              </p>
            </div>
            <Link className="module-action" href="/transferencias">
              Abrir módulo <ArrowIcon />
            </Link>
          </article>

          <article className="module-card">
            <span className="module-icon module-icon-blue">
              <ActivityIcon />
            </span>
            <div>
              <span className="module-state">Monitoramento operacional</span>
              <h3>Observabilidade</h3>
              <p>
                Liveness, readiness, latência instantânea, SLOs e diagnóstico.
              </p>
            </div>
            <Link className="module-action" href="/observabilidade">
              Abrir módulo <ArrowIcon />
            </Link>
          </article>
        </div>
      </section>

      <aside className="portfolio-disclaimer">
        <strong>BankFlow é um laboratório autoral de engenharia.</strong>
        <span>Transações simuladas, sem dinheiro real e sem vínculo com o Nubank.</span>
      </aside>
    </main>
  );
}
