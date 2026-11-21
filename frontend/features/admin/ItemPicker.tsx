"use client";

import { useMemo, useState } from "react";
import type { CatalogItem } from "@/features/admin/types";

/** Foto pequena do item (do cliente do jogo). Sem imagem, um quadrado vazio do mesmo tamanho. */
export function ItemThumb({ id, image, size = 32 }: { id: string; image: boolean; size?: number }) {
  return image ? (
    // eslint-disable-next-line @next/next/no-img-element -- imagem .bmp do cliente, servida como está
    <img src={`/item-imagem/${id}`} alt="" width={size} height={size} loading="lazy" className="shrink-0 object-contain [image-rendering:pixelated]" style={{ width: size, height: size }} />
  ) : (
    <span aria-hidden className="inline-block shrink-0 rounded-sm border border-steel/60 bg-night/60" style={{ width: size, height: size }} />
  );
}

const MaxShown = 120;

/**
 * Campo do código do item com "Escolher pela foto": abre uma grade com foto e nome dos itens do servidor;
 * clicar preenche o código (em minúsculas quando a tabela usa minúsculas, como "ca105").
 */
export function ItemPicker({
  name,
  label,
  defaultValue,
  error,
  catalog,
  lowercase,
  maxLength,
}: {
  name: string;
  label: string;
  defaultValue?: string | null;
  error?: string;
  catalog: CatalogItem[];
  lowercase: boolean;
  maxLength?: number;
}) {
  const [value, setValue] = useState(defaultValue ?? "");
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const byId = useMemo(() => new Map(catalog.map((i) => [i.id, i])), [catalog]);
  const selected = byId.get(value.trim().toUpperCase());
  const found = useMemo(() => {
    const q = query.trim().toLowerCase();
    const list = q ? catalog.filter((i) => i.id.toLowerCase().startsWith(q) || (i.name ?? "").toLowerCase().includes(q)) : catalog;
    return [...list].sort((a, b) => Number(b.image) - Number(a.image));
  }, [catalog, query]);
  const id = `f-${name}`;

  return (
    <div className="flex flex-col gap-1 sm:col-span-2">
      <label htmlFor={id} className="text-sm text-silver">
        {label}
      </label>
      <div className="flex flex-wrap items-center gap-2">
        <input
          id={id}
          name={name}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={maxLength}
          aria-invalid={error ? true : undefined}
          className={`w-32 rounded-sm border bg-night/80 px-3 py-2 font-mono text-sm text-silver outline-none focus-visible:border-arcane ${error ? "border-data-bad" : "border-bronze/70"}`}
        />
        <button type="button" onClick={() => setOpen((o) => !o)} className="rounded-sm border border-arcane/70 px-3 py-2 text-sm text-arcane hover:bg-arcane/10" aria-expanded={open}>
          {open ? "Fechar" : "Escolher pela foto"}
        </button>
        {selected ? (
          <span className="flex items-center gap-2 text-sm text-data-name">
            <ItemThumb id={selected.id} image={selected.image} />
            {selected.name ?? selected.id}
          </span>
        ) : value.trim() ? (
          <span className="text-xs text-silver/50">código fora da lista do servidor</span>
        ) : null}
      </div>
      {error ? <p className="text-xs text-data-bad">{error}</p> : null}
      {open ? (
        <div className="mt-2 rounded-sm border border-steel bg-night/90 p-2">
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Procurar pelo nome ou código (ex.: traje, ca1)"
            aria-label="Procurar item"
            className="mb-2 w-full rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver outline-none focus-visible:border-arcane"
          />
          {catalog.length === 0 ? (
            <p className="p-2 text-xs text-silver/60">A lista de itens do servidor não está disponível.</p>
          ) : (
            <ul className="grid max-h-80 grid-cols-2 gap-1 overflow-y-auto sm:grid-cols-3 lg:grid-cols-4">
              {found.slice(0, MaxShown).map((i) => (
                <li key={i.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setValue(lowercase ? i.id.toLowerCase() : i.id);
                      setOpen(false);
                    }}
                    className="flex w-full items-center gap-2 rounded-sm border border-transparent p-1.5 text-left hover:border-gold/60 hover:bg-steel/30"
                  >
                    <ItemThumb id={i.id} image={i.image} size={36} />
                    <span className="min-w-0">
                      <span className="block truncate text-xs text-silver">{i.name ?? "Sem nome"}</span>
                      <span className="block font-mono text-[11px] text-silver/50">{lowercase ? i.id.toLowerCase() : i.id}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {found.length > MaxShown ? <p className="mt-1 text-xs text-silver/50">Mostrando {MaxShown} de {found.length}. Digite para filtrar.</p> : null}
        </div>
      ) : null}
    </div>
  );
}
