import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { ClassBadge } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import type { SearchResult } from "@/features/ranking/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Personagens", description: "Busque personagens do NexusPK." };

async function Results({ searchParams }: { searchParams: Promise<{ q?: string | string[] }> }) {
  await connection();
  const { q } = await searchParams;
  const query = typeof q === "string" ? q.trim().slice(0, 32) : "";
  if (query.length === 0) return <p className="text-sm text-silver/70">Digite pelo menos 2 letras do nome do personagem.</p>;
  if (query.length < 2) return <p className="text-sm text-silver/70">Digite pelo menos 2 letras.</p>;
  const result = await apiSend<SearchResult[]>("GET", `/api/v1/characters?q=${encodeURIComponent(query)}`);
  if (!result.ok) return <p className="text-sm text-data-bad">Busca indisponível no momento.</p>;
  if (result.data.length === 0) return <p className="text-sm text-silver/70">Nenhum personagem encontrado com “{query}”.</p>;
  return (
    <table className="w-full text-sm">
      <caption className="sr-only">Resultado da busca</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="py-1 font-normal">Classe</th>
          <th scope="col" className="py-1 text-right font-normal">Nível</th>
          <th scope="col" className="py-1 text-right font-normal">Posição</th>
        </tr>
      </thead>
      <tbody>
        {result.data.map((c) => (
          <tr key={c.name} className="border-t border-steel/60">
            <td className="py-1.5">
              <Link href={`/personagens/${encodeURIComponent(c.name)}`} className="font-medium text-data-name hover:underline">
                {c.name}
              </Link>
            </td>
            <td className="py-1.5 text-silver/80">
              <ClassBadge code={c.classCode} name={c.className} />
            </td>
            <td className="tabular py-1.5 text-right text-data-info">{c.level}</td>
            <td className="tabular py-1.5 text-right text-silver/70">{c.position}º</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

async function SearchBox({ searchParams }: { searchParams: Promise<{ q?: string | string[] }> }) {
  const { q } = await searchParams;
  return (
    <form method="get" action="/personagens" className="mb-4 flex gap-2" role="search">
      <label htmlFor="busca" className="sr-only">
        Nome do personagem
      </label>
      <input
        id="busca"
        name="q"
        defaultValue={typeof q === "string" ? q : ""}
        minLength={2}
        maxLength={32}
        placeholder="Nome do personagem"
        className="min-w-0 flex-1 rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane focus-visible:ring-1 focus-visible:ring-arcane"
      />
      <button type="submit" className="btn-glow rounded-sm border border-arcane/60 bg-gradient-to-b from-arcane to-[#2f7fd1] px-4 py-2 text-sm font-semibold text-night">
        Buscar
      </button>
    </form>
  );
}

export default function CharactersPage({ searchParams }: PageProps<"/personagens">) {
  const sp = searchParams as Promise<{ q?: string | string[] }>;
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Personagens</h1>
      <GameWindow title="Buscar personagem">
        <Suspense fallback={null}>
          <SearchBox searchParams={sp} />
        </Suspense>
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
          <Results searchParams={sp} />
        </Suspense>
      </GameWindow>
    </div>
  );
}
