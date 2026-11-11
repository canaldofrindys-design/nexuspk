"use client";

import { useEffect, useState } from "react";

function parts(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000));
  return { d: Math.floor(s / 86400), h: Math.floor((s % 86400) / 3600), m: Math.floor((s % 3600) / 60), s: s % 60 };
}

const pad = (n: number) => String(n).padStart(2, "0");

/**
 * Contagem regressiva até `target`. O HTML do servidor já vem com o valor do momento da página;
 * no navegador o número segue o relógio local a cada segundo.
 */
export function Countdown({ target, label, serverNow }: { target: string; label: string; serverNow: string }) {
  const end = new Date(target).getTime();
  const [now, setNow] = useState(() => new Date(serverNow).getTime());
  useEffect(() => {
    const tick = () => setNow(Date.now());
    const first = setTimeout(tick, 0);
    const id = setInterval(tick, 1000);
    return () => {
      clearTimeout(first);
      clearInterval(id);
    };
  }, []);

  const left = end - now;
  if (left <= 0) return <p className="text-sm text-silver/70">{label}: agora.</p>;
  const p = parts(left);
  return (
    <p className="text-sm text-silver">
      {label}{" "}
      <span className="tabular text-lg text-data-name" role="timer" aria-live="off">
        {p.d > 0 ? `${p.d}d ` : ""}
        {pad(p.h)}:{pad(p.m)}:{pad(p.s)}
      </span>
    </p>
  );
}
