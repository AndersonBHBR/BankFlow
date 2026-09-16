import { ObservabilityConsole } from "@/features/observability/observability-console";
import { getPlatformObservability } from "@/lib/bankflow-api";

export const dynamic = "force-dynamic";

export default async function ObservabilityPage() {
  const initialSnapshot = await getPlatformObservability();

  return (
    <ObservabilityConsole
      initialSnapshot={initialSnapshot}
      links={{
        aspireDashboard:
          process.env.ASPIRE_DASHBOARD_PUBLIC_URL ?? "http://localhost:18888",
        rabbitMqManagement:
          process.env.RABBITMQ_MANAGEMENT_PUBLIC_URL ?? "http://localhost:15672",
      }}
    />
  );
}
