"use client";

import { useMemo, useState } from "react";
import type { MixRecipe } from "@/features/site/types";
import { mixTargetLabel, mixTargets } from "./labels";

/** Mix List com filtro por equipamento e busca por nome, sheltom ou efeito. Um quadro por tipo de mix. */
export function MixTable({ items }: { items: MixRecipe[] }) {
  const [target, setTarget] = useState<string>("");
  const [query, setQuery] = useState("");
  const present = useMemo(() => Object.keys(mixTargets).filter((k) => items.some((i) => i.target === k)), [items]);
  const filtered = useMemo(() => {
    const q = normalize(query);
    return items.filter(
      (i) => (!target || i.target === target) && (!q || [i.name, i.sheltoms, i.effect, i.group].some((t) => normalize(t).includes(q))),
    );
  }, [items, target, query]);
  const groups = useMemo(() => {
    const map = new Map<string, MixRecipe[]>();
    for (const i of filtered) map.set(i.group, [...(map.get(i.group) ?? []), i]);
    return [...map.entries()];
  }, [filtered]);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div role="group" aria-label="Equipamento" className="flex flex-wrap gap-1.5">
          {[["", "Todos"] as const, ...present.map((k) => [k, mixTargetLabel(k)] as const)].map(([k, label]) => (
            <button
              key={k || "todos"}
              type="button"
              aria-pressed={target === k}
              onClick={() => setTarget(k)}
              className={`border px-3 py-1 text-sm ${target === k ? "border-gold bg-gold/15 text-gold-bright" : "border-steel text-silver/80 hover:border-arcane hover:text-arcane"}`}
            >
              {label}
            </button>
          ))}
        </div>
        <label className="flex flex-col gap-1 text-xs text-silver/70 md:w-72">
          Buscar mix, sheltom ou efeito
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Ex.: Devine, crítico, defesa"
            className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver"
          />
        </label>
      </div>

      {groups.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum mix com esse filtro.</p>
      ) : (
        groups.map(([group, list]) => (
          <section key={group} aria-labelledby={`mix-${slug(group)}`} className="game-window px-3 py-3">
            <h2 id={`mix-${slug(group)}`} className="mb-2 px-1 font-display text-lg font-bold text-gold-bright">
              {group}
            </h2>
            <div className="guide-table-wrap">
              <table className="guide-table">
                <thead>
                  <tr>
                    <th scope="col">Mix</th>
                    <th scope="col">Equipamento</th>
                    <th scope="col">Sheltoms</th>
                    <th scope="col">O que dá</th>
                  </tr>
                </thead>
                <tbody>
                  {list.map((r, i) => (
                    <tr key={`${r.name}-${r.target}-${i}`}>
                      <th scope="row">{r.name}</th>
                      <td>{mixTargetLabel(r.target)}</td>
                      <td>{r.sheltoms}</td>
                      <td>{r.effect}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ))
      )}
    </div>
  );
}

function normalize(t: string) {
  return t.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase().trim();
}

function slug(t: string) {
  return normalize(t).replace(/[^a-z0-9]+/g, "-");
}
