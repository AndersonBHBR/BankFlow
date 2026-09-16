import { redirect } from "next/navigation";
import { Brand } from "@/components/brand";
import { getCurrentUser } from "@/lib/auth";
import { LoginForm } from "@/app/login/login-form";

export default async function LoginPage() {
  const user = await getCurrentUser();
  if (user) {
    redirect("/dashboard");
  }

  return (
    <main className="login-page">
      <section className="login-intro">
        <Brand />
        <div className="intro-content">
          <span className="eyebrow">Core banking distribuído, visão unificada</span>
          <h1>Movimente dinheiro com segurança e rastreabilidade.</h1>
          <p>
            Contas, Pix, limites, lançamentos e saúde operacional em um laboratório
            completo de arquitetura bancária orientada a eventos.
          </p>
        </div>

        <div className="architecture-strip" aria-label="Fluxo da plataforma">
          <span>Gateway</span>
          <i />
          <span>Transferências</span>
          <i />
          <span>Mensageria</span>
          <i />
          <span>Contas</span>
        </div>

        <p className="independent-note">Projeto educacional independente. Não afiliado ao Nubank ou a qualquer instituição financeira.</p>
      </section>

      <section className="login-panel">
        <div className="login-card">
          <div className="login-card-heading">
            <span className="section-index">Acesso seguro</span>
            <h2>Bem-vindo ao BankFlow</h2>
            <p>Entre com um usuário habilitado para acessar o console.</p>
          </div>

          <LoginForm />

          <aside className="demo-credentials">
            <span>Credencial administrativa de demonstração</span>
            <strong>admin@bankflow.local</strong>
            <code>BankFlow#2026</code>
          </aside>
        </div>

        <p className="environment-caption">
          Ambiente local · .NET 10 · Next.js 16 · OpenTelemetry
        </p>
      </section>
    </main>
  );
}
