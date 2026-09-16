"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { LogoutIcon } from "@/components/icons";

export function LogoutButton() {
  const [isPending, setIsPending] = useState(false);
  const router = useRouter();

  async function logout() {
    setIsPending(true);
    try {
      await fetch("/api/session", { method: "DELETE" });
      router.replace("/login");
      router.refresh();
    } finally {
      setIsPending(false);
    }
  }

  return (
    <button
      className="logout-button"
      type="button"
      onClick={logout}
      disabled={isPending}
    >
      <LogoutIcon />
      <span>{isPending ? "Encerrando…" : "Sair"}</span>
    </button>
  );
}
