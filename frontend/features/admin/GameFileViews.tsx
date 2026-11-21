"use client";

import { useMemo, useState } from "react";
import { ItemThumb } from "@/features/admin/ItemPicker";
import type { CatalogItem, MonsterView, NpcShopView } from "@/features/admin/types";

const fmt = new Intl.NumberFormat("pt-BR");

function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (v: string) => void; placeholder: string }) {
  return (
    <input
      type="search"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      aria-label={placeholder}
      className="mb-3 w-full rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane sm:max-w-md"
    />
  );
}

const MaxItems = 240;

/** Catálogo de itens do servidor (pasta OpenItem) com a foto do cliente. Só consulta. */
export function ItemCatalogView({ items }: { items: CatalogItem[] }) {
  const [q, setQ] = useState("");
  const found = useMemo(() => {
    const t = q.trim().toLowerCase();
    return t ? items.filter((i) => i.id.toLowerCase().startsWith(t) || (i.name ?? "").toLowerCase().includes(t)) : items;
  }, [items, q]);
  return (
    <div>
      <SearchBox value={q} onChange={setQ} placeholder="Procurar pelo nome ou código (ex.: traje, wa1)" />
      <p className="mb-2 text-xs text-silver/55">
        {fmt.format(found.length)} de {fmt.format(items.length)} itens{found.length > MaxItems ? ` (mostrando ${MaxItems}; digite para filtrar)` : ""}.
      </p>
      <ul className="grid grid-cols-1 gap-1.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {found.slice(0, MaxItems).map((i) => (
          <li key={i.id} className="flex items-center gap-2 rounded-sm border border-steel/60 bg-night/50 p-2">
            <ItemThumb id={i.id} image={i.image} size={40} />
            <span className="min-w-0">
              <span className="block truncate text-sm text-silver">{i.name ?? "Sem nome"}</span>
              <span className="block font-mono text-xs text-silver/50">{i.id}</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

const MaxMonsters = 60;

/** Drops de cada monstro, lidos dos arquivos do servidor. Busca pelo monstro ou pelo item. Só consulta. */
export function MonsterDropsView({ monsters }: { monsters: MonsterView[] }) {
  const [q, setQ] = useState("");
  const [onlyItems, setOnlyItems] = useState(true);
  const found = useMemo(() => {
    const t = q.trim().toLowerCase();
    return monsters.filter((m) => {
      if (onlyItems && !m.drops.some((d) => d.items.length > 0)) return false;
      if (!t) return true;
      return m.name.toLowerCase().includes(t) || m.drops.some((d) => d.items.some((i) => i.id.toLowerCase().startsWith(t) || (i.name ?? "").toLowerCase().includes(t)));
    });
  }, [monsters, q, onlyItems]);
  return (
    <div>
      <div className="flex flex-wrap items-center gap-3">
        <SearchBox value={q} onChange={setQ} placeholder="Procurar monstro ou item (ex.: valento, traje)" />
        <label className="mb-3 flex items-center gap-2 text-sm text-silver/70">
          <input type="checkbox" checked={onlyItems} onChange={(e) => setOnlyItems(e.target.checked)} />
          Só monstros que derrubam item
        </label>
      </div>
      <p className="mb-2 text-xs text-silver/55">
        {fmt.format(found.length)} monstros{found.length > MaxMonsters ? ` (mostrando ${MaxMonsters}; digite para filtrar)` : ""}.
      </p>
      <ul className="flex flex-col gap-2">
        {found.slice(0, MaxMonsters).map((m) => (
          <li key={m.file} className="rounded-sm border border-steel/60 bg-night/50 p-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-gold-bright">{m.name}</span>
              <span className="text-xs text-silver/50">
                {m.hp != null ? `${fmt.format(m.hp)} de vida · ` : ""}
                <span className="font-mono">{m.file}</span>
              </span>
            </div>
            <ul className="mt-2 flex flex-col gap-1.5">
              {m.drops.map((d, i) => (
                <li key={i} className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="w-16 shrink-0 tabular-nums text-data-info">{d.chance}%</span>
                  {d.gold != null ? <span className="text-gold">Ouro {d.gold}</span> : null}
                  {d.items.map((it) => (
                    <span key={it.id} className="flex items-center gap-1.5 rounded-sm border border-steel/50 px-1.5 py-0.5" title={it.id}>
                      <ItemThumb id={it.id} image={it.image} size={24} />
                      <span className="text-xs text-silver">{it.name ?? it.id}</span>
                    </span>
                  ))}
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
    </div>
  );
}

const shopGroups = [
  { key: "weapons", label: "Armas" },
  { key: "armors", label: "Armaduras" },
  { key: "misc", label: "Diversos" },
] as const;

/** Lojas dos NPCs, lidas dos arquivos do servidor. Busca pelo NPC ou pelo item. Só consulta. */
export function NpcShopsView({ shops }: { shops: NpcShopView[] }) {
  const [q, setQ] = useState("");
  const found = useMemo(() => {
    const t = q.trim().toLowerCase();
    if (!t) return shops;
    return shops.filter(
      (s) =>
        s.name.toLowerCase().includes(t) ||
        s.file.toLowerCase().includes(t) ||
        shopGroups.some((g) => s[g.key].some((i) => i.id.toLowerCase().startsWith(t) || (i.name ?? "").toLowerCase().includes(t))),
    );
  }, [shops, q]);
  return (
    <div>
      <SearchBox value={q} onChange={setQ} placeholder="Procurar NPC ou item (ex.: sabrine, poção, ec101)" />
      <p className="mb-2 text-xs text-silver/55">
        {fmt.format(found.length)} de {fmt.format(shops.length)} NPCs que vendem itens.
      </p>
      <ul className="flex flex-col gap-2">
        {found.map((s) => (
          <li key={s.file} className="rounded-sm border border-steel/60 bg-night/50 p-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-gold-bright">{s.name}</span>
              <span className="font-mono text-xs text-silver/50">{s.file}</span>
            </div>
            <div className="mt-2 flex flex-col gap-1.5">
              {shopGroups
                .filter((g) => s[g.key].length > 0)
                .map((g) => (
                  <div key={g.key} className="flex flex-wrap items-center gap-2 text-sm">
                    <span className="w-20 shrink-0 text-data-info">{g.label}</span>
                    {s[g.key].map((it) => (
                      <span key={it.id} className="flex items-center gap-1.5 rounded-sm border border-steel/50 px-1.5 py-0.5" title={it.id}>
                        <ItemThumb id={it.id} image={it.image} size={24} />
                        <span className="text-xs text-silver">{it.name ?? it.id}</span>
                      </span>
                    ))}
                  </div>
                ))}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
