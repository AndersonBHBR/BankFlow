import type { ServiceHealth } from "@/lib/contracts";

type ServiceStatusCardProps = {
  service: ServiceHealth;
};

export function ServiceStatusCard({ service }: ServiceStatusCardProps) {
  const isOperational = service.status === "operational";

  return (
    <article className="service-card">
      <div className="service-card-header">
        <span className={`status-dot ${isOperational ? "is-up" : "is-down"}`} />
        <span className={`status-label ${isOperational ? "is-up" : "is-down"}`}>
          {isOperational ? "Operacional" : "Indisponível"}
        </span>
        <span className="latency">
          {service.latencyMs === null ? "—" : `${service.latencyMs} ms`}
        </span>
      </div>
      <h3>{service.name}</h3>
      <p>{service.description}</p>
    </article>
  );
}
