"use client";

import { useEffect, type ReactNode } from "react";
import { CloseIcon } from "@/components/icons";

type SidePanelProps = {
  title: string;
  eyebrow: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
};

export function SidePanel({
  title,
  eyebrow,
  children,
  onClose,
  wide = false,
}: SidePanelProps) {
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

  return (
    <div className="panel-backdrop" role="presentation" onMouseDown={onClose}>
      <section
        className={`side-panel ${wide ? "side-panel-wide" : ""}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="side-panel-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="side-panel-header">
          <div>
            <span className="eyebrow">{eyebrow}</span>
            <h2 id="side-panel-title">{title}</h2>
          </div>
          <button
            type="button"
            className="icon-button"
            aria-label="Fechar painel"
            onClick={onClose}
          >
            <CloseIcon />
          </button>
        </header>
        <div className="side-panel-body">{children}</div>
      </section>
    </div>
  );
}
