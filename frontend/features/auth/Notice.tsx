import type { ReactNode } from "react";

/** Aviso dentro de uma janela: função indisponível, link inválido etc. */
export function Notice({ tone = "info", children }: { tone?: "info" | "bad"; children: ReactNode }) {
  const color = tone === "bad" ? "text-data-bad" : "text-data-info";
  return <p className={`text-sm ${color}`}>{children}</p>;
}
