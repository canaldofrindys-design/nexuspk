"use client";

import Image from "next/image";
import { useState } from "react";

export type DropView = {
  item: string;
  icon: string;
  width: number;
  height: number;
  player: string;
  boss: string | null;
  when: string;
};

function describe(d: DropView) {
  return `${d.item}, dropado por ${d.player}${d.boss ? ` no ${d.boss}` : ""} em ${d.when}`;
}

/**
 * Faixa "Drops raros da semana": cada espaço é um drop anunciado pela equipe. Passar o mouse, focar ou
 * tocar num espaço mostra, logo abaixo, de qual boss caiu e quem pegou (funciona igual no celular).
 */
export function RareDropsStrip({ drops, slots }: { drops: DropView[]; slots: number }) {
  const [selected, setSelected] = useState(0);
  const current = drops[selected] ?? drops[0];
  const empty = Math.max(0, slots - drops.length);
  // No celular (6 colunas) só completa a última fileira; no computador, os 11 espaços.
  const mobileFill = (6 - (drops.length % 6)) % 6;
  return (
    <div>
      <ul className="grid grid-cols-6 gap-1.5 sm:grid-cols-11" aria-label="Drops raros da semana">
        {drops.map((d, i) => (
          <li key={`${d.item}-${d.player}-${i}`}>
            <button
              type="button"
              className={`nx-slot nx-drop ${i === selected ? "nx-drop--on" : ""}`}
              aria-pressed={i === selected}
              aria-label={describe(d)}
              title={describe(d)}
              onMouseEnter={() => setSelected(i)}
              onFocus={() => setSelected(i)}
              onClick={() => setSelected(i)}
            >
              <Image
                src={`/kit/prancha/item-${d.icon}.webp`}
                alt=""
                width={d.width}
                height={d.height}
                unoptimized
                className="h-auto w-full object-contain"
              />
            </button>
          </li>
        ))}
        {Array.from({ length: empty }, (_, i) => (
          <li key={`vazio-${i}`} aria-hidden className={i >= mobileFill ? "max-sm:hidden" : undefined}>
            <span className="nx-slot nx-slot--empty" />
          </li>
        ))}
      </ul>
      {current ? (
        <p className="nx-drop-detail" aria-live="polite">
          <span className="text-gold-bright">{current.item}</span>
          <span className="text-silver/70"> dropado por </span>
          <span className="text-data-name">{current.player}</span>
          {current.boss ? (
            <>
              <span className="text-silver/70"> no </span>
              <span className="text-data-bad">{current.boss}</span>
            </>
          ) : null}
          <span className="text-silver/50"> · {current.when}</span>
        </p>
      ) : null}
    </div>
  );
}
