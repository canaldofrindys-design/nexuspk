import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { itemCodePattern, type ItemGuidePage, type ItemStat } from "@/features/items/types";
import { apiSend } from "@/services/api";

async function load(code: string) {
  if (!itemCodePattern.test(code)) return null;
  const result = await apiSend<ItemGuidePage>("GET", `/api/v1/items/${code.toUpperCase()}`, {});
  if (!result.ok && result.status === 404) return null;
  return result;
}

export async function generateMetadata({ params }: PageProps<"/itens/[codigo]">): Promise<Metadata> {
  const result = await load((await params).codigo);
  return result?.ok ? { title: result.data.name, description: `${result.data.name}: atributos, de onde cai e quem vende no NexusPK.` } : { title: "Item" };
}

function StatList({ items }: { items: ItemStat[] }) {
  return (
    <dl className="grid grid-cols-[1fr_auto] gap-x-4 gap-y-1.5 text-sm">
      {items.map((s) => (
        <div key={s.label} className="contents">
          <dt className="text-silver/70">{s.label}</dt>
          <dd className="text-right tabular-nums text-silver">{s.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Chance de cair este item numa morte: a da linha de drop dividida pelos itens que ela sorteia. */
function itemChance(chance: string, choices: number) {
  const c = Number(chance);
  if (!Number.isFinite(c) || choices <= 1) return null;
  const each = c / choices;
  return each >= 1 ? `${each.toFixed(1).replace(".", ",")}%` : `${each.toFixed(2).replace(".", ",")}%`;
}

async function Detail({ params }: { params: Promise<{ codigo: string }> }) {
  await connection();
  const result = await load((await params).codigo);
  if (!result) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Item indisponível no momento. Tente de novo em instantes.</p>;
  const d = result.data;
  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_340px]">
      <article className="game-window self-start">
        <header className="game-window__title flex items-center gap-4 px-4 py-3">
          <span className="nx-slot--item w-20 shrink-0">
            {d.image ? (
              // eslint-disable-next-line @next/next/no-img-element -- ícone .bmp do cliente do jogo, servido como está
              <img src={`/item-imagem/${d.id}`} alt="" />
            ) : null}
          </span>
          <span>
            <h1 className="text-xl text-gold-bright">{d.name}</h1>
            <span className="font-mono text-xs text-silver/50">{d.id}</span>
          </span>
        </header>
        <div className="flex flex-col gap-5 p-4">
          <section aria-labelledby="sec-drop">
            <h2 id="sec-drop" className="mb-2 text-sm text-gold">Onde cai</h2>
            {d.drops.length ? (
              <ul className="flex flex-col gap-1.5 text-sm">
                {d.drops.map((s) => {
                  const each = itemChance(s.chance, s.choices);
                  return (
                    <li key={`${s.monster}-${s.chance}-${s.choices}`} className="flex flex-wrap items-baseline justify-between gap-2 border-b border-steel/40 pb-1.5">
                      <span className="text-data-bad">{s.monster}</span>
                      <span className="text-xs text-silver/70">
                        {s.chance}% de drop{s.choices > 1 ? `, sorteado entre ${s.choices} itens` : ""}
                        {each ? <span className="text-data-info"> (≈ {each} por morte)</span> : null}
                      </span>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <p className="text-sm text-silver/60">Nenhum monstro derruba este item.</p>
            )}
          </section>
          {d.shops.length ? (
            <section aria-labelledby="sec-shop">
              <h2 id="sec-shop" className="mb-2 text-sm text-gold">Onde comprar</h2>
              <ul className="flex flex-col gap-1 text-sm">
                {d.shops.map((s) => (
                  <li key={`${s.npc}-${s.tab}`}>
                    <span className="text-silver">{s.npc}</span> <span className="text-xs text-silver/60">({s.tab})</span>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
          <p className="text-xs text-silver/50">Informações lidas direto dos arquivos do servidor do jogo.</p>
        </div>
      </article>

      <div className="flex flex-col gap-4">
        <GameWindow title="Atributos">
          {d.stats.length ? <StatList items={d.stats} /> : <p className="text-sm text-silver/60">Sem atributos no arquivo do item.</p>}
        </GameWindow>
        {d.requirements.length ? (
          <GameWindow title="Requisitos">
            <StatList items={d.requirements} />
          </GameWindow>
        ) : null}
        {d.spec.length ? (
          <GameWindow title="Bônus da classe">
            <StatList items={d.spec} />
          </GameWindow>
        ) : null}
        <Link href="/" className="text-sm text-arcane hover:underline">
          Voltar para o início
        </Link>
      </div>
    </div>
  );
}

export default function ItemPage({ params }: PageProps<"/itens/[codigo]">) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <Suspense fallback={<div className="game-window h-64 animate-pulse" aria-label="Carregando item" />}>
        <Detail params={params} />
      </Suspense>
    </div>
  );
}
