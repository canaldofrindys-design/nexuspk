import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatNumber } from "@/features/home/format";
import { percent, type MonsterPage } from "@/features/monsters/types";
import { apiSend } from "@/services/api";

async function load(slug: string) {
  if (slug.length === 0 || slug.length > 120) return null;
  const result = await apiSend<MonsterPage>("GET", `/api/v1/monsters/${encodeURIComponent(slug)}`);
  if (!result.ok && result.status === 404) return null;
  return result;
}

export async function generateMetadata({ params }: PageProps<"/monstros/[slug]">): Promise<Metadata> {
  const result = await load(decodeURIComponent((await params).slug));
  return result?.ok ? { title: result.data.name, description: `${result.data.name}: nível, vida e drops no NexusPK.` } : { title: "Monstro" };
}

async function Detail({ params }: { params: Promise<{ slug: string }> }) {
  await connection();
  const result = await load(decodeURIComponent((await params).slug));
  if (!result) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Monstro indisponível no momento. Tente de novo em instantes.</p>;
  const m = result.data;
  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_300px]">
      <article className="game-window self-start">
        <header className="game-window__title px-4 py-3">
          <h1 className="text-xl text-data-bad">{m.name}</h1>
        </header>
        <div className="flex flex-col gap-4 p-4">
          {m.drops.map((line, i) => (
            <section key={i} aria-label={`Drop ${i + 1}`} className="border-b border-steel/40 pb-3 last:border-0">
              <p className="mb-2 text-xs text-silver/70">
                <span className="text-gold">{percent(line.chance)}</span> de chance
                {line.choices > 1 ? `, sorteia 1 entre ${line.choices} itens` : ""}
              </p>
              {line.gold !== null ? (
                <p className="text-sm text-gold">Ouro{line.gold ? `: ${line.gold.replace("–", " a ")}` : ""}</p>
              ) : null}
              {line.items.length ? (
                <ul className="grid gap-1.5 sm:grid-cols-2">
                  {line.items.map((it) => (
                    <li key={it.id}>
                      <Link href={`/itens/${it.id}`} className="flex items-center gap-2 rounded-sm p-1 text-sm hover:bg-steel/30">
                        <span className="nx-slot--item w-9 shrink-0">
                          {it.image ? (
                            // eslint-disable-next-line @next/next/no-img-element -- ícone .bmp do cliente do jogo, servido como está
                            <img src={`/item-imagem/${it.id}`} alt="" loading="lazy" />
                          ) : null}
                        </span>
                        <span className="min-w-0">
                          <span className="block truncate text-silver">{it.name}</span>
                          {it.perKill ? <span className="text-xs text-data-info">≈ {percent(it.perKill)} por morte</span> : null}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              ) : null}
            </section>
          ))}
          <p className="text-xs text-silver/50">Informações lidas direto dos arquivos do servidor do jogo.</p>
        </div>
      </article>
      <div className="flex flex-col gap-4">
        <GameWindow title="Atributos" icon="boss">
          <dl className="grid grid-cols-[1fr_auto] gap-x-4 gap-y-1.5 text-sm">
            <dt className="text-silver/70">Nível</dt>
            <dd className="tabular text-right text-data-info">{m.level ?? "—"}</dd>
            <dt className="text-silver/70">Vida</dt>
            <dd className="tabular text-right text-data-info">{m.hp != null ? formatNumber(m.hp) : "—"}</dd>
            {m.exp != null ? (
              <>
                <dt className="text-silver/70">EXP base</dt>
                <dd className="tabular text-right text-data-info">{formatNumber(m.exp)}</dd>
              </>
            ) : null}
          </dl>
        </GameWindow>
        <Link href="/monstros" className="text-sm text-arcane hover:underline">
          Todos os monstros
        </Link>
      </div>
    </div>
  );
}

export default function MonsterDetailPage({ params }: PageProps<"/monstros/[slug]">) {
  return (
    <div data-scene="torre-vigia" className="mx-auto max-w-6xl px-4 py-8">
      <Suspense fallback={<div className="game-window h-64 animate-pulse" aria-label="Carregando monstro" />}>
        <Detail params={params} />
      </Suspense>
    </div>
  );
}
