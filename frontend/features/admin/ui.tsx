import type { ReactNode } from "react";

const pillTones = {
  ok: "border-data-ok/50 bg-data-ok/10 text-data-ok",
  bad: "border-data-bad/50 bg-data-bad/10 text-data-bad",
  warn: "border-data-name/50 bg-data-name/10 text-data-name",
  info: "border-data-info/50 bg-data-info/10 text-data-info",
  mute: "border-steel bg-night/60 text-silver/60",
} as const;

export type PillTone = keyof typeof pillTones;

export function Pill({ tone, children }: { tone: PillTone; children: ReactNode }) {
  return <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs ${pillTones[tone]}`}>{children}</span>;
}

/** Número em destaque, no estilo dos cartões do resumo. */
export function Stat({ label, value, note }: { label: string; value: ReactNode; note?: string }) {
  return (
    <div className="rounded-md border border-steel/70 bg-night/50 px-3 py-2.5">
      <p className="text-xs text-silver/55">{label}</p>
      <p className="tabular mt-0.5 text-lg text-silver">{value}</p>
      {note ? <p className="text-xs text-silver/45">{note}</p> : null}
    </div>
  );
}

/** Aviso para função desligada (falta configuração na VPS) ou erro da API. */
export function Notice({ tone = "warn", title, children }: { tone?: "warn" | "bad" | "info"; title?: string; children: ReactNode }) {
  const c = tone === "bad" ? "border-data-bad/50 text-data-bad" : tone === "info" ? "border-data-info/50 text-data-info" : "border-data-name/50 text-data-name";
  return (
    <div className={`rounded-md border bg-night/60 px-4 py-3 text-sm ${c}`}>
      {title ? <p className="mb-1 font-semibold">{title}</p> : null}
      <div className="text-silver/80">{children}</div>
    </div>
  );
}

export function PageTitle({ title, children, actions }: { title: string; children?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="font-display text-2xl text-gold-bright">{title}</h1>
        {children ? <p className="mt-1 max-w-3xl text-sm text-silver/65">{children}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
    </div>
  );
}
