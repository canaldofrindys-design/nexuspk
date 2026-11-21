"use client";

import { useEffect, useState } from "react";

/** "2d 04h", "3h 12min", "12min 05s". Passou do horário: "agora". */
export function formatCountdown(ms: number): string {
  if (ms <= 0) return "agora";
  const s = Math.floor(ms / 1000);
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const pad = (n: number) => String(n).padStart(2, "0");
  if (d > 0) return `${d}d ${pad(h)}h`;
  if (h > 0) return `${h}h ${pad(m)}min`;
  return `${m}min ${pad(s % 60)}s`;
}

/** Contagem regressiva até `to` (ISO). Atualiza a cada segundo; o servidor manda o primeiro valor. */
export function Countdown({ to, prefix = "" }: { to: string; prefix?: string }) {
  const target = Date.parse(to);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);
  return (
    <time dateTime={to} suppressHydrationWarning className="tabular">
      {prefix}
      {formatCountdown(target - now)}
    </time>
  );
}
