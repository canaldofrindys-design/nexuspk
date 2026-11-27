import { PageHero } from "@/components/scene/PageHero";
import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense, type ReactNode } from "react";
import { ClassBadge, ClassIcon } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { KitIcon, PodiumMedal } from "@/components/ui/KitIcon";
import { ClanEmblem } from "@/features/home/ClanEmblem";
import { classes } from "@/features/home/classes";
import { formatNumber, formatTime } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import type { RankingPage, SodClanEntry, SodEntry, SodMonths } from "@/features/ranking/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = {
  title: "Ranking do SoD",
  description: "Ranking do Survival of the Death (Bellatra) do NexusPK: melhores jogadores do mês e da semana e os clãs com mais pontos.",
};

type Kind = "mensal" | "semanal" | "clas";
type Query = { tipo?: string; mes?: string; classe?: string; pagina?: string };

function parse(q: Query) {
  const kind: Kind = q.tipo === "semanal" || q.tipo === "clas" ? q.tipo : "mensal";
  const month = /^\d{4}-(0[1-9]|1[0-2])$/.test(q.mes ?? "") ? q.mes : undefined;
  const cls = Number(q.classe);
  const classCode = Number.isInteger(cls) && cls >= 1 && cls <= 11 ? cls : undefined;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  return { kind, month, classCode, page };
}

type State = ReturnType<typeof parse>;

function url(s: State, change: Partial<State>) {
  const n = { ...s, page: 1, ...change };
  const p = new URLSearchParams();
  if (n.kind !== "mensal") p.set("tipo", n.kind);
  if (n.kind === "mensal" && n.month) p.set("mes", n.month);
  if (n.kind !== "clas" && n.classCode) p.set("classe", String(n.classCode));
  if (n.page > 1) p.set("pagina", String(n.page));
  const qs = p.toString();
  return qs ? `/ranking/sod?${qs}` : "/ranking/sod";
}

/** "2026-11-01" → "novembro de 2026". */
function monthLabel(isoDate: string) {
  const [y, m] = isoDate.split("-").map(Number);
  const text = new Intl.DateTimeFormat("pt-BR", { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(Date.UTC(y, m - 1, 1)));
  return text.charAt(0).toUpperCase() + text.slice(1);
}

const crownName: Record<number, string> = { 1: "Coroa de ouro", 2: "Coroa de prata", 3: "Coroa de bronze" };
const crownStyle: Record<number, string> = {
  1: "border-gold/70 text-gold-bright",
  2: "border-silver/60 text-silver",
  3: "border-bronze/80 text-bronze",
};

function Position({ n }: { n: number }) {
  return (
    <td className={`tabular w-12 py-1.5 ${n <= 3 ? "text-gold-bright" : "text-silver/70"}`}>
      {n <= 3 ? <PodiumMedal position={n} /> : n}
    </td>
  );
}

function CharacterLink({ name }: { name: string }) {
  return (
    <Link href={`/personagens/${encodeURIComponent(name)}`} className="font-medium text-data-name hover:text-gold-bright hover:underline">
      {name}
    </Link>
  );
}

function PlayerTable({ data, caption }: { data: RankingPage<SodEntry>; caption: string }) {
  return (
    <table className="w-full min-w-[520px] text-sm">
      <caption className="sr-only">{caption}</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">#</th>
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="py-1 font-normal">Classe</th>
          <th scope="col" className="py-1 font-normal">Clã</th>
          <th scope="col" className="py-1 text-right font-normal">Nível</th>
          <th scope="col" className="py-1 text-right font-normal">Pontos</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((c) => (
          <tr key={c.name} className="border-t border-steel/60">
            <Position n={c.position} />
            <td className="py-1.5">
              <CharacterLink name={c.name} />
            </td>
            <td className="py-1.5 text-silver/80">
              <ClassBadge code={c.classCode} name={c.className} />
            </td>
            <td className="py-1.5 text-silver/80">{c.clan ?? "—"}</td>
            <td className="tabular py-1.5 text-right text-data-info">{c.level > 0 ? c.level : "—"}</td>
            <td className="tabular py-1.5 text-right font-semibold text-gold-bright">{formatNumber(c.points)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ClanTable({ data }: { data: RankingPage<SodClanEntry> }) {
  return (
    <table className="w-full min-w-[560px] text-sm">
      <caption className="sr-only">Ranking de clãs no SoD</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">#</th>
          <th scope="col" className="py-1 font-normal">Clã</th>
          <th scope="col" className="py-1 font-normal">Líder</th>
          <th scope="col" className="py-1 text-right font-normal">Membros</th>
          <th scope="col" className="py-1 text-right font-normal">Pontos no SoD</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((c) => (
          <tr key={c.name} className="border-t border-steel/60">
            <Position n={c.position} />
            <td className="py-1.5">
              <span className="inline-flex flex-wrap items-center gap-2">
                <ClanEmblem iconId={c.iconId} name={c.name} size={28} />
                <span className="font-medium text-data-name">{c.name}</span>
                {c.crown ? (
                  <span className={`border px-1 text-xs ${crownStyle[c.crown]}`} title="Coroa dada pelo SoD aos três primeiros clãs">
                    {crownName[c.crown]}
                  </span>
                ) : null}
              </span>
            </td>
            <td className="py-1.5 text-silver/80">{c.leader ? <CharacterLink name={c.leader} /> : "—"}</td>
            <td className="tabular py-1.5 text-right text-data-info">{formatNumber(c.members)}</td>
            <td className="tabular py-1.5 text-right font-semibold text-gold-bright">{formatNumber(c.points)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ClassTabs({ s }: { s: State }) {
  return (
    <Tabs
      label="Classe"
      tabs={[{ code: undefined, name: "Todas" }, ...classes].map((c) => ({
        href: url(s, { classCode: c.code }),
        label: c.name,
        icon: c.code ? <ClassIcon code={c.code} name={c.name} size={20} /> : undefined,
        active: s.classCode === c.code,
      }))}
    />
  );
}

const unavailable = <p className="text-sm text-data-bad">Ranking indisponível no momento. Tente de novo em instantes.</p>;

function Footer({ generatedUtc, children }: { generatedUtc: string; children?: ReactNode }) {
  return (
    <p className="mt-3 text-xs text-silver/50">
      Atualizado às {formatTime(generatedUtc)}. Os dados do jogo são relidos a cada 5 minutos. Contas de GM não entram.{children}
    </p>
  );
}

async function MonthlyBody({ s }: { s: State }) {
  const months = await apiSend<SodMonths>("GET", "/api/v1/rankings/sod/meses");
  const q = new URLSearchParams({ tipo: "mensal", pagina: String(s.page), porPagina: "50" });
  if (s.month) q.set("mes", s.month);
  if (s.classCode) q.set("classe", String(s.classCode));
  const result = await apiSend<RankingPage<SodEntry>>("GET", `/api/v1/rankings/sod?${q}`);

  const current = months.ok ? months.data.current.slice(0, 7) : undefined;
  const list = months.ok ? [...new Set([months.data.current, ...months.data.months].map((m) => m.slice(0, 7)))] : [];
  const shown = s.month ?? current;
  if (shown && !list.includes(shown)) list.push(shown);
  const title = result.ok && result.data.periodStart ? `Solo: ${monthLabel(result.data.periodStart)}` : "Solo do mês";

  return (
    <GameWindow title={title} icon="trofeu">
      <div className="mb-3 flex flex-col gap-2">
        {list.length > 1 ? (
          <Tabs
            label="Mês"
            tabs={list.map((m) => ({
              href: url(s, { month: m === current ? undefined : m }),
              label: m === current ? "Este mês" : monthLabel(`${m}-01`),
              active: shown === m,
            }))}
          />
        ) : null}
        <ClassTabs s={s} />
      </div>
      {!result.ok ? (
        unavailable
      ) : (
        <>
          {!result.data.available ? (
            <p className="text-sm text-silver/80">
              O servidor ainda não gravou o SoD deste mês. A tabela do mês aparece depois da primeira partida em Bellatra; se já houve
              partida, o portal pode estar sem leitura no banco do SoD.
            </p>
          ) : result.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Ninguém pontuou no SoD neste mês ainda.</p>
          ) : (
            <div className="overflow-x-auto">
              <PlayerTable data={result.data} caption="Ranking solo do SoD no mês" />
            </div>
          )}
          <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(page) => url(s, { page })} />
          <Footer generatedUtc={result.data.generatedUtc}> Vale a maior pontuação de cada personagem no mês, como o ranking Bellatra do jogo.</Footer>
        </>
      )}
    </GameWindow>
  );
}

async function WeeklyBody({ s }: { s: State }) {
  const q = new URLSearchParams({ tipo: "semanal", pagina: String(s.page), porPagina: "50" });
  if (s.classCode) q.set("classe", String(s.classCode));
  const result = await apiSend<RankingPage<SodEntry>>("GET", `/api/v1/rankings/sod?${q}`);
  return (
    <GameWindow title="Solo: semana" icon="rankingSemanal">
      <div className="mb-3">
        <ClassTabs s={s} />
      </div>
      {!result.ok ? (
        unavailable
      ) : (
        <>
          {result.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Ninguém pontuou no SoD nesta semana ainda.</p>
          ) : (
            <div className="overflow-x-auto">
              <PlayerTable data={result.data} caption="Ranking solo do SoD na semana" />
            </div>
          )}
          <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(page) => url(s, { page })} />
          <Footer generatedUtc={result.data.generatedUtc}> Os pontos da semana são o contador do próprio jogo, zerado no reset semanal.</Footer>
        </>
      )}
    </GameWindow>
  );
}

async function ClanBody({ s }: { s: State }) {
  const q = new URLSearchParams({ pagina: String(s.page), porPagina: "50" });
  const result = await apiSend<RankingPage<SodClanEntry>>("GET", `/api/v1/rankings/sod/clas?${q}`);
  return (
    <GameWindow title="Clãs" icon="guildasBandeira">
      {!result.ok ? (
        unavailable
      ) : (
        <>
          {result.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Nenhum clã pontuou no SoD ainda.</p>
          ) : (
            <div className="overflow-x-auto">
              <ClanTable data={result.data} />
            </div>
          )}
          <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(page) => url(s, { page })} />
          <Footer generatedUtc={result.data.generatedUtc}> Pontos de clã que o jogo soma a cada SoD. Os três primeiros levam as coroas de ouro, prata e bronze, com bônus de HP.</Footer>
        </>
      )}
    </GameWindow>
  );
}

async function SodBody({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const s = parse(await searchParams);
  return s.kind === "clas" ? <ClanBody s={s} /> : s.kind === "semanal" ? <WeeklyBody s={s} /> : <MonthlyBody s={s} />;
}

async function KindTabs({ searchParams }: { searchParams: Promise<Query> }) {
  const s = parse(await searchParams);
  return (
    <Tabs
      label="Ranking do SoD"
      tabs={[
        { href: url(s, { kind: "mensal" }), label: "Solo do mês", icon: <KitIcon name="trofeu" size={24} />, active: s.kind === "mensal" },
        { href: url(s, { kind: "semanal" }), label: "Solo da semana", icon: <KitIcon name="rankingSemanal" size={24} />, active: s.kind === "semanal" },
        { href: url(s, { kind: "clas" }), label: "Clãs", icon: <KitIcon name="guildasBandeira" size={24} />, active: s.kind === "clas" },
      ]}
    />
  );
}

export default function SodRankingPage({ searchParams }: PageProps<"/ranking/sod">) {
  const sp = searchParams as Promise<Query>;
  return (
    <>
      <PageHero
        scene="torre-vigia"
        icon="trofeu"
        eyebrow="Survival of the Death"
        title="Ranking do SoD"
        lead="Quem mais pontuou em Bellatra no mês e na semana, e os clãs que disputam as coroas de ouro, prata e bronze. Direto do servidor."
        aside={
          <Link href="/manual#pvp" className="nx-btn btn-glow inline-flex items-center px-4 py-2 font-display text-sm font-bold tracking-wide uppercase">
            Como funciona
          </Link>
        }
      />
      <div className="mx-auto w-full max-w-7xl px-4 py-10">
        <div className="mb-4 flex flex-col gap-3">
          <Tabs
            label="Rankings"
            tabs={[
              { href: "/ranking", label: "Nível", icon: <KitIcon name="rankingGeral" size={24} />, active: false },
              { href: "/ranking?tipo=pvp", label: "PvP", icon: <KitIcon name="pvp" size={24} />, active: false },
              { href: "/ranking?tipo=clas", label: "Clãs", icon: <KitIcon name="guildasBandeira" size={24} />, active: false },
              { href: "/ranking/sod", label: "SoD", icon: <KitIcon name="trofeu" size={24} />, active: true },
            ]}
          />
          <Suspense fallback={null}>
            <KindTabs searchParams={sp} />
          </Suspense>
        </div>
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
          <SodBody searchParams={sp} />
        </Suspense>
      </div>
    </>
  );
}
