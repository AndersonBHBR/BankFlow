"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowIcon, ShieldIcon } from "@/components/icons";

type LoginError = {
  message?: string;
};

export function LoginForm() {
  const [login, setLogin] = useState("admin@bankflow.local");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);
  const router = useRouter();

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setIsPending(true);

    try {
      const response = await fetch("/api/session", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ login, password }),
      });

      if (!response.ok) {
        const error = (await response.json()) as LoginError;
        setMessage(error.message ?? "Não foi possível entrar.");
        return;
      }

      router.replace("/dashboard");
      router.refresh();
    } catch {
      setMessage("Falha de comunicação com o portal.");
    } finally {
      setIsPending(false);
    }
  }

  return (
    <form className="login-form" onSubmit={submit}>
      <div className="field-group">
        <label htmlFor="login">E-mail corporativo</label>
        <input
          id="login"
          name="login"
          type="email"
          autoComplete="username"
          value={login}
          onChange={(event) => setLogin(event.target.value)}
          required
        />
      </div>

      <div className="field-group">
        <div className="field-label-row">
          <label htmlFor="password">Senha</label>
          <span>Ambiente demonstrativo</span>
        </div>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />
      </div>

      {message && (
        <p className="form-error" role="alert">
          {message}
        </p>
      )}

      <button className="primary-button" type="submit" disabled={isPending}>
        <span>{isPending ? "Validando acesso…" : "Acessar console"}</span>
        {!isPending && <ArrowIcon />}
      </button>

      <div className="security-note">
        <ShieldIcon />
        <p>
          A credencial é processada pelo BFF. O token permanece protegido em
          cookie <code>HttpOnly</code> e não é exposto ao navegador.
        </p>
      </div>
    </form>
  );
}
