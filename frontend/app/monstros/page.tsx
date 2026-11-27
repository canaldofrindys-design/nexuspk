import { PageHero } from "@/components/scene/PageHero";
import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatNumber } from "@/features/home/format";
import type { MonsterList } from "@/features/monsters/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = {
  title: "Monstros",
  description: "Banco de monstros do NexusPK: nível, vida e tudo que cada monstro derruba, lido direto dos arquivos do servidor.",
};

type Query = { q?: string | string[] };

function queryOf(q: Query["q"]) {
  return typeof q === "string" ? q.trim().slice(0, 40) : "";
}

async function Results({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const query = queryOf((await searchParams).q);
  const result = await apiSend<MonsterList>("GET", `/api/v1/monsters${query ? `?q=${encodeURIComponent(query)}` : ""}`);
  if (!result.ok) return <p className="text-sm text-data-bad">Banco de monstros indisponível no momento. Tente de novo em instantes.</p>;
  const d = result.data;
  if (!d.available) return <p className="text-sm text-silver/70">A lista de monstros ainda não está disponível: o portal não conseguiu ler os arquivos do servidor.</p>;
  if (d.monsters.length === 0) return <p className="text-sm text-silver/70">Nenhum monstro derruba ou se chama “{query}”.</p>;
  return (
    <>
      <p className="mb-2 text-xs text-silver/60">
        {query ? `${d.monsters.length} de ${formatNumber(d.total)} monstros` : `${formatNumber(d.total)} monstros`}, do menor nível para o maior.
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[480px] text-sm">
          <caption className="sr-only">Monstros</caption>
          <thead>
            <tr className="text-left text-xs text-silver/60">
              <th scope="col" className="py-1 font-normal">Monstro</th>
              <th scope="col" className="py-1 text-right font-normal">Nível</th>
              <th scope="col" className="py-1 text-right font-normal">Vida</th>
              <th scope="col" className="py-1 text-right font-normal">Itens no drop</th>
            </tr>
          </thead>
          <tbody>
            {d.monsters.map((m) => (
              <tr key={m.slug} className="border-t border-steel/60">
                <td className="py-1.5">
                  <Link href={`/monstros/${encodeURIComponent(m.slug)}`} className="font-medium text-data-bad hover:text-gold-bright hover:underline">
                    {m.name}
                  </Link>
                </td>
                <td className="tabular py-1.5 text-right text-data-info">{m.level ?? "—"}</td>
                <td className="tabular py-1.5 text-right text-data-info">{m.hp != null ? formatNumber(m.hp) : "—"}</td>
                <td className="tabular py-1.5 text-right text-silver/80">
                  {m.items}
                  {m.gold ? <span className="text-gold"> + ouro</span> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

async function SearchBox({ searchParams }: { searchParams: Promise<Query> }) {
  const query = queryOf((await searchParams).q);
  return (
    <form method="get" action="/monstros" className="mb-4 flex gap-2" role="search">
      <label htmlFor="busca-monstro" className="sr-only">
        Monstro ou item
      </label>
      <input
        id="busca-monstro"
        name="q"
        defaultValue={query}
        maxLength={40}
        placeholder="Nome do monstro ou do item"
        className="min-w-0 flex-1 rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane focus-visible:ring-1 focus-visible:ring-arcane"
      />
      <button type="submit" className="btn-glow rounded-sm border border-arcane/60 bg-gradient-to-b from-arcane to-[#2f7fd1] px-4 py-2 text-sm font-semibold text-night">
        Buscar
      </button>
    </form>
  );
}

export default function MonstersPage({ searchParams }: PageProps<"/monstros">) {
  const sp = searchParams as Promise<Query>;
  return (
    <>
      <PageHero
        scene="torre-vigia"
        icon="boss"
        eyebrow="Banco de monstros"
        title="Monstros"
        lead="Nível, vida e tudo que cada monstro derruba, com a chance de cada item. Procure pelo monstro ou pelo item que você quer."
      />
      <div className="mx-auto w-full max-w-4xl px-4 py-10">
        <GameWindow title="Monstros do servidor" icon="boss">
          <Suspense fallback={null}>
            <SearchBox searchParams={sp} />
          </Suspense>
          <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
            <Results searchParams={sp} />
          </Suspense>
        </GameWindow>
      </div>
    </>
  );
}
