import { PageHero } from "@/components/scene/PageHero";
import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { ClassBadge } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { KitIcon } from "@/components/ui/KitIcon";
import { formatDateTime } from "@/features/home/format";
import type { HistoryPage, ItemHistoryEvent, LevelUpEvent } from "@/features/history/types";
import { dayMonth } from "@/features/ranking/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import { apiSend } from "@/services/api";

export const metadata: Metadata = {
  title: "Históricos",
  description: "Quem subiu de nível, fez aging e craft no NexusPK nos últimos 30 dias.",
};

type Kind = "nivel" | "aging" | "craft";
type Query = { tipo?: string; pagina?: string };

function parse(q: Query) {
  const kind: Kind = q.tipo === "aging" || q.tipo === "craft" ? q.tipo : "nivel";
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  return { kind, page };
}

function url(kind: Kind, page = 1) {
  const p = new URLSearchParams();
  if (kind !== "nivel") p.set("tipo", kind);
  if (page > 1) p.set("pagina", String(page));
  const qs = p.toString();
  return qs ? `/historico?${qs}` : "/historico";
}

function CharacterLink({ name }: { name: string }) {
  return (
    <Link href={`/personagens/${encodeURIComponent(name)}`} className="font-medium text-data-name hover:text-gold-bright hover:underline">
      {name}
    </Link>
  );
}

function when(e: LevelUpEvent) {
  if (e.today) return e.sinceDay ? `desde ${dayMonth(e.sinceDay)}` : "hoje";
  return e.sinceDay ? `entre ${dayMonth(e.sinceDay)} e ${dayMonth(e.day)}` : dayMonth(e.day);
}

function LevelTable({ data }: { data: HistoryPage<LevelUpEvent> }) {
  if (data.items.length === 0) {
    return (
      <p className="text-sm text-silver/80">
        Ninguém subiu de nível desde a primeira foto do portal. O portal tira uma foto dos níveis todo dia logo após a meia-noite (horário de
        Brasília) e compara uma foto com a outra; não existe histórico anterior à primeira foto.
      </p>
    );
  }
  return (
    <table className="w-full text-sm">
      <caption className="sr-only">Subidas de nível</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">Quando</th>
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="hidden py-1 font-normal sm:table-cell">Classe</th>
          <th scope="col" className="py-1 text-right font-normal">Nível</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((e) => (
          <tr key={`${e.name}-${e.day}-${e.toLevel}`} className="border-t border-steel/60">
            <td className="tabular py-1.5 text-silver/70">{when(e)}</td>
            <td className="py-1.5">
              <CharacterLink name={e.name} />
            </td>
            <td className="hidden py-1.5 text-silver/80 sm:table-cell">
              <ClassBadge code={e.classCode} name={e.className} />
            </td>
            <td className="tabular py-1.5 text-right">
              <span className="text-silver/60">{e.fromLevel} →</span> <span className="text-data-info">{e.toLevel}</span>
              <span className="ml-1 text-xs text-gold">+{e.toLevel - e.fromLevel}</span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ItemTable({ data, kind }: { data: HistoryPage<ItemHistoryEvent>; kind: "aging" | "craft" }) {
  const label = kind === "aging" ? "aging" : "craft";
  if (!data.configured) {
    return (
      <p className="text-sm text-silver/80">
        O histórico de {label} ainda não foi ligado pela equipe do servidor. Assim que for, cada {label} feito no jogo aparece aqui.
      </p>
    );
  }
  if (data.items.length === 0) return <p className="text-sm text-silver/80">Nenhum {label} nos últimos 30 dias.</p>;
  return (
    <table className="w-full text-sm">
      <caption className="sr-only">Histórico de {label}</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">Quando</th>
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="py-1 font-normal">Item</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((e, i) => (
          <tr key={`${e.character}-${e.atUtc}-${i}`} className="border-t border-steel/60">
            <td className="tabular py-1.5 text-silver/70">{formatDateTime(e.atUtc)}</td>
            <td className="py-1.5">
              <CharacterLink name={e.character} />
            </td>
            <td className="py-1.5">
              {e.itemId ? (
                <Link href={`/itens/${e.itemId}`} className="inline-flex items-center gap-2 text-silver hover:underline">
                  <span className="nx-slot--item w-7 shrink-0">
                    {/* eslint-disable-next-line @next/next/no-img-element -- ícone .bmp do cliente do jogo, servido como está */}
                    <img src={`/item-imagem/${e.itemId}`} alt="" loading="lazy" />
                  </span>
                  {e.item}
                </Link>
              ) : (
                <span className="text-silver">{e.item}</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

async function HistoryBody({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const s = parse(await searchParams);
  const path = s.kind === "nivel" ? "/api/v1/history/levels" : `/api/v1/history/${s.kind}`;
  const result = await apiSend<HistoryPage<LevelUpEvent & ItemHistoryEvent>>("GET", `${path}?pagina=${s.page}&porPagina=50`);
  const title = s.kind === "nivel" ? "Subidas de nível" : s.kind === "aging" ? "Aging" : "Craft";
  return (
    <GameWindow title={title} icon={s.kind === "nivel" ? "rankingGeral" : "recompensas"}>
      {!result.ok || !result.data.available ? (
        <p className="text-sm text-data-bad">Histórico indisponível no momento. Tente de novo em instantes.</p>
      ) : (
        <>
          {result.data.since && result.data.configured ? <p className="mb-2 text-xs text-silver/60">Últimos 30 dias, desde {dayMonth(result.data.since)}. O mais novo primeiro.</p> : null}
          <div className="overflow-x-auto">
            {s.kind === "nivel" ? <LevelTable data={result.data} /> : <ItemTable data={result.data} kind={s.kind} />}
          </div>
          <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(p) => url(s.kind, p)} />
        </>
      )}
    </GameWindow>
  );
}

async function KindTabs({ searchParams }: { searchParams: Promise<Query> }) {
  const s = parse(await searchParams);
  return (
    <Tabs
      label="Históricos"
      tabs={[
        { href: url("nivel"), label: "Níveis", icon: <KitIcon name="rankingGeral" size={24} />, active: s.kind === "nivel" },
        { href: url("aging"), label: "Aging", icon: <KitIcon name="recompensas" size={24} />, active: s.kind === "aging" },
        { href: url("craft"), label: "Craft", icon: <KitIcon name="loja" size={24} />, active: s.kind === "craft" },
      ]}
    />
  );
}

export default function HistoryPageRoute({ searchParams }: PageProps<"/historico">) {
  const sp = searchParams as Promise<Query>;
  return (
    <>
      <PageHero
        scene="jornada"
        icon="noticias"
        eyebrow="O que acontece no vale"
        title="Históricos"
        lead="Quem subiu de nível, fez aging e craft nos últimos 30 dias, direto do servidor. A equipe do servidor fica de fora."
      />
      <div className="mx-auto w-full max-w-4xl px-4 py-10">
        <Suspense fallback={null}>
          <div className="mb-4">
            <KindTabs searchParams={sp} />
          </div>
        </Suspense>
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
          <HistoryBody searchParams={sp} />
        </Suspense>
      </div>
    </>
  );
}
