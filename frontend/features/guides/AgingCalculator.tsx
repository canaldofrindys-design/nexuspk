"use client";

import { useState } from "react";
import { agingFailFrom, agingMax, sheltomOrder, sheltomsBetween } from "./aging";

const levels = Array.from({ length: agingMax + 1 }, (_, i) => i);

/** Soma os sheltoms de todos os níveis entre o aging atual do item e o desejado. */
export function AgingCalculator() {
  const [from, setFrom] = useState(0);
  const [to, setTo] = useState(10);
  const target = Math.max(to, from + 1 > agingMax ? agingMax : from + 1);
  const total = sheltomsBetween(from, target);
  const list = sheltomOrder.filter((n) => (total[n] ?? 0) > 0);
  const count = list.reduce((sum, n) => sum + (total[n] ?? 0), 0);
  const select = "rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver tabular";
  return (
    <div className="flex flex-col gap-3 border border-steel/60 p-3">
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs text-silver/70">
          Aging do item hoje
          <select
            value={from}
            onChange={(e) => {
              const v = Number(e.target.value);
              setFrom(v);
              if (to <= v) setTo(Math.min(agingMax, v + 1));
            }}
            className={select}
          >
            {levels.slice(0, agingMax).map((l) => (
              <option key={l} value={l}>
                {l === 0 ? "Sem aging" : `+${l}`}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-silver/70">
          Quero chegar em
          <select value={target} onChange={(e) => setTo(Number(e.target.value))} className={select}>
            {levels.slice(from + 1).map((l) => (
              <option key={l} value={l}>
                +{l}
              </option>
            ))}
          </select>
        </label>
      </div>
      <p className="text-sm text-silver/85" aria-live="polite">
        Do {from === 0 ? "item sem aging" : `+${from}`} ao +{target}: <span className="font-semibold text-gold-bright tabular">{count} sheltoms</span> em{" "}
        {target - from} {target - from === 1 ? "nível" : "níveis"}.
      </p>
      <ul className="grid grid-cols-2 gap-1.5 sm:grid-cols-3 md:grid-cols-4">
        {list.map((n) => (
          <li key={n} className="flex items-center justify-between border border-steel/50 px-2 py-1 text-sm">
            <span className="text-silver">{n}</span>
            <span className="tabular text-data-info">×{total[n]}</span>
          </li>
        ))}
      </ul>
      {target >= agingFailFrom ? (
        <p className="text-xs text-data-bad">
          Do +{agingFailFrom} em diante o aging pode falhar: a conta acima vale se tudo der certo na primeira tentativa. Separe sheltoms e gold de reserva.
        </p>
      ) : (
        <p className="text-xs text-silver/60">Até o +{agingFailFrom - 1} o aging não falha. Cada nível também cobra uma taxa em gold.</p>
      )}
    </div>
  );
}
