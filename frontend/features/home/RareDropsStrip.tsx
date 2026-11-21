"use client";

import Image from "next/image";
import { useState } from "react";

/**
 * Um espaço da faixa. kind "drop": alguém pegou o item nesta semana. kind "showcase": item real do jogo que cai de boss,
 * mostrado nos espaços que sobram. code: código do arquivo do item ("CA182"), com a imagem real copiada do cliente.
 */
export type DropView = {
  kind: "drop" | "showcase";
  item: string;
  code: string | null;
  icon: string | null;
  width: number;
  height: number;
  player: string | null;
  boss: string | null;
  when: string | null;
};

function describe(d: DropView) {
  if (d.kind === "showcase") return `${d.item}${d.boss ? `, cai de ${d.boss}` : ""}`;
  return `${d.item}, dropado por ${d.player}${d.boss ? ` no ${d.boss}` : ""} em ${d.when}`;
}

function SlotArt({ d }: { d: DropView }) {
  if (d.code) {
    return (
      <span className="nx-slot--item">
        {/* eslint-disable-next-line @next/next/no-img-element -- ícone .bmp do cliente do jogo, servido como está */}
        <img src={`/item-imagem/${d.code}`} alt="" loading="lazy" />
      </span>
    );
  }
  return d.icon ? (
    <Image src={`/kit/prancha/item-${d.icon}.webp`} alt="" width={d.width} height={d.height} unoptimized className="h-auto w-full object-contain" />
  ) : (
    <span className="nx-slot--empty" />
  );
}

/**
 * Faixa "Drops raros da semana": cada espaço é um drop real (do log de itens) e, nos que sobram, itens reais que caem
 * dos bosses. Passar o mouse, focar ou tocar num espaço mostra os detalhes logo abaixo (funciona igual no celular).
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
          <li key={`${d.kind}-${d.code ?? d.item}-${d.player ?? ""}-${i}`}>
            <button
              type="button"
              className={`nx-slot nx-drop ${i === selected ? "nx-drop--on" : ""} ${d.kind === "showcase" ? "nx-drop--showcase" : ""}`}
              aria-pressed={i === selected}
              aria-label={describe(d)}
              title={describe(d)}
              onMouseEnter={() => setSelected(i)}
              onFocus={() => setSelected(i)}
              onClick={() => setSelected(i)}
            >
              <SlotArt d={d} />
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
          {current.kind === "showcase" ? (
            current.boss ? (
              <>
                <span className="text-silver/70"> cai de </span>
                <span className="text-data-bad">{current.boss}</span>
              </>
            ) : null
          ) : (
            <>
              <span className="text-silver/70"> dropado por </span>
              <span className="text-data-name">{current.player}</span>
              {current.boss ? (
                <>
                  <span className="text-silver/70"> no </span>
                  <span className="text-data-bad">{current.boss}</span>
                </>
              ) : null}
              <span className="text-silver/50"> · {current.when}</span>
            </>
          )}
        </p>
      ) : null}
    </div>
  );
}
