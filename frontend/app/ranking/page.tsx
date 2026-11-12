import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { ClassBadge, ClassIcon } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "@/features/home/classes";
import { formatDate, formatNumber, formatTime } from "@/features/home/format";
import { dayMonth, periodLabel, type Period } from "@/features/ranking/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import type { ClanEntry, LevelEntry, PvpEntry, RankingPage } from "@/features/ranking/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Ranking", description: "Ranking de nível, PvP e clãs do NexusPK." };

type Kind = "nivel" | "pvp" | "clas";
type PvpKind = "bp" | "semanal" | "abates";
type Query = { tipo?: string; periodo?: string; pvp?: string; classe?: string; pagina?: string };

function parse(q: Query) {
  const kind: Kind = q.tipo === "pvp" || q.tipo === "clas" ? q.tipo : "nivel";
  const period: Period = q.periodo === "diario" || q.periodo === "semanal" || q.periodo === "mensal" ? q.periodo : "geral";
  const pvp: PvpKind = q.pvp === "semanal" || q.pvp === "abates" ? q.pvp : "bp";
  const cls = Number(q.classe);
  const classCode = Number.isInteger(cls) && cls >= 1 && cls <= 10 ? cls : undefined;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  return { kind, period, pvp, classCode, page };
}

type State = ReturnType<typeof parse>;

function url(s: State, change: Partial<State>) {
  const n = { ...s, page: 1, ...change };
  const p = new URLSearchParams();
  if (n.kind !== "nivel") p.set("tipo", n.kind);
  if (n.kind === "nivel" && n.period !== "geral") p.set("periodo", n.period);
  if (n.kind === "pvp" && n.pvp !== "bp") p.set("pvp", n.pvp);
  if (n.kind !== "clas" && n.classCode) p.set("classe", String(n.classCode));
  if (n.page > 1) p.set("pagina", String(n.page));
  const qs = p.toString();
  return qs ? `/ranking?${qs}` : "/ranking";
}

function CharacterLink({ name }: { name: string }) {
  return (
    <Link href={`/personagens/${encodeURIComponent(name)}`} className="font-medium text-data-name hover:text-gold-bright hover:underline">
      {name}
    </Link>
  );
}

function Position({ n }: { n: number }) {
  return <td className={`tabular w-10 py-1.5 ${n <= 3 ? "text-gold-bright" : "text-silver/70"}`}>{n}</td>;
}

function LevelTable({ data, period }: { data: RankingPage<LevelEntry>; period: Period }) {
  const gains = period !== "geral";
  return (
    <table className="w-full min-w-[520px] text-sm">
      <caption className="sr-only">Ranking por nível</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">#</th>
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="py-1 font-normal">Classe</th>
          <th scope="col" className="py-1 font-normal">Clã</th>
          {gains ? <th scope="col" className="py-1 text-right font-normal">Níveis ganhos</th> : null}
          <th scope="col" className="py-1 text-right font-normal">Nível</th>
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
            {gains ? <td className="tabular py-1.5 text-right text-data-ok">+{c.levelsGained}</td> : null}
            <td className="tabular py-1.5 text-right text-data-info">{c.level}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function PvpTable({ data }: { data: RankingPage<PvpEntry> }) {
  return (
    <table className="w-full min-w-[560px] text-sm">
      <caption className="sr-only">Ranking PvP</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">#</th>
          <th scope="col" className="py-1 font-normal">Personagem</th>
          <th scope="col" className="py-1 font-normal">Classe</th>
          <th scope="col" className="py-1 text-right font-normal">BP</th>
          <th scope="col" className="py-1 text-right font-normal">BP da semana</th>
          <th scope="col" className="py-1 text-right font-normal">Abates</th>
          <th scope="col" className="py-1 text-right font-normal">Mortes</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((c) => (
          <tr key={c.name} className="border-t border-steel/60">
            <Position n={c.position} />
            <td className="py-1.5">
              <CharacterLink name={c.name} />
              {c.isPvpKing ? <span className="ml-2 border border-gold/60 px-1 text-xs text-gold-bright">Rei do PvP</span> : null}
            </td>
            <td className="py-1.5 text-silver/80">
              <ClassBadge code={c.classCode} name={c.className} />
            </td>
            <td className="tabular py-1.5 text-right text-data-info">{formatNumber(c.battlePoints)}</td>
            <td className="tabular py-1.5 text-right text-data-info">{formatNumber(c.weeklyBattlePoints)}</td>
            <td className="tabular py-1.5 text-right text-data-info">{formatNumber(c.kills)}</td>
            <td className="tabular py-1.5 text-right text-silver/70">{formatNumber(c.deaths)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ClanTable({ data }: { data: RankingPage<ClanEntry> }) {
  return (
    <table className="w-full min-w-[520px] text-sm">
      <caption className="sr-only">Ranking de clãs</caption>
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">#</th>
          <th scope="col" className="py-1 font-normal">Clã</th>
          <th scope="col" className="py-1 text-right font-normal">Pontos</th>
          <th scope="col" className="py-1 text-right font-normal">Membros</th>
          <th scope="col" className="py-1 text-right font-normal">Vitórias / derrotas</th>
          <th scope="col" className="py-1 text-right font-normal">Criado em</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((c) => (
          <tr key={c.name} className="border-t border-steel/60">
            <Position n={c.position} />
            <td className="py-1.5 font-medium text-data-name">{c.name}</td>
            <td className="tabular py-1.5 text-right text-data-info">{formatNumber(c.points)}</td>
            <td className="tabular py-1.5 text-right text-data-info">{c.members}</td>
            <td className="tabular py-1.5 text-right text-data-info">
              {c.wins} / {c.losses}
            </td>
            <td className="tabular py-1.5 text-right text-silver/70">{c.createdUtc ? formatDate(c.createdUtc) : "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function PeriodNote({ data, period }: { data: RankingPage<unknown>; period: Period }) {
  if (period === "geral") return null;
  if (!data.available) {
    return (
      <p className="text-sm text-silver/80">
        Ainda não há foto de nível para este período. O portal tira uma foto dos níveis todo dia logo após a meia-noite (horário de
        Brasília), e os rankings de hoje, da semana e do mês contam a partir dela. Não existe histórico anterior à primeira foto.
      </p>
    );
  }
  if (data.countingSince && data.periodStart && data.countingSince !== data.periodStart) {
    return (
      <p className="mb-2 text-xs text-silver/60">
        Contando desde {dayMonth(data.countingSince)}, a primeira foto de nível do portal neste período (o período começou em{" "}
        {dayMonth(data.periodStart)}).
      </p>
    );
  }
  return data.periodStart ? <p className="mb-2 text-xs text-silver/60">Contando desde {dayMonth(data.periodStart)}.</p> : null;
}

async function RankingBody({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const s = parse(await searchParams);
  const common = new URLSearchParams({ pagina: String(s.page), porPagina: "50" });
  if (s.classCode && s.kind !== "clas") common.set("classe", String(s.classCode));

  const path =
    s.kind === "nivel"
      ? `/api/v1/rankings/level?periodo=${s.period}&${common}`
      : s.kind === "pvp"
        ? `/api/v1/rankings/pvp?tipo=${s.pvp}&${common}`
        : `/api/v1/rankings/clans?${common}`;
  const result = await apiSend<RankingPage<LevelEntry & PvpEntry & ClanEntry>>("GET", path);

  const title = s.kind === "nivel" ? `Nível: ${periodLabel[s.period]}` : s.kind === "pvp" ? "PvP" : "Clãs";
  return (
    <GameWindow title={title}>
      <div className="mb-3 flex flex-col gap-2">
        {s.kind === "nivel" ? (
          <Tabs
            label="Período"
            tabs={(Object.keys(periodLabel) as Period[]).map((p) => ({ href: url(s, { period: p }), label: periodLabel[p], active: s.period === p }))}
          />
        ) : null}
        {s.kind === "pvp" ? (
          <Tabs
            label="Critério"
            tabs={[
              { href: url(s, { pvp: "bp" }), label: "BP total", active: s.pvp === "bp" },
              { href: url(s, { pvp: "semanal" }), label: "BP da semana", active: s.pvp === "semanal" },
              { href: url(s, { pvp: "abates" }), label: "Abates", active: s.pvp === "abates" },
            ]}
          />
        ) : null}
        {s.kind !== "clas" ? (
          <Tabs
            label="Classe"
            tabs={[{ code: undefined, name: "Todas" }, ...classes].map((c) => ({
              href: url(s, { classCode: c.code }),
              label: c.name,
              icon: c.code ? <ClassIcon code={c.code} name={c.name} size={20} /> : undefined,
              active: s.classCode === c.code,
            }))}
          />
        ) : null}
      </div>

      {!result.ok ? (
        <p className="text-sm text-data-bad">Ranking indisponível no momento. Tente de novo em instantes.</p>
      ) : (
        <>
          {s.kind === "nivel" ? <PeriodNote data={result.data} period={s.period} /> : null}
          {result.data.available && result.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Ninguém neste ranking ainda.</p>
          ) : result.data.items.length > 0 ? (
            <div className="overflow-x-auto">
              {s.kind === "nivel" ? (
                <LevelTable data={result.data} period={s.period} />
              ) : s.kind === "pvp" ? (
                <PvpTable data={result.data} />
              ) : (
                <ClanTable data={result.data} />
              )}
            </div>
          ) : null}
          <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(page) => url(s, { page })} />
          <p className="mt-3 text-xs text-silver/50">
            Atualizado às {formatTime(result.data.generatedUtc)}. Os dados do jogo são relidos a cada 5 minutos. Contas de GM não entram.
            {s.kind === "nivel" ? " Classificação por nível; EXP entra quando o servidor passar a publicá-la." : null}
            {s.kind === "pvp" ? " O BP da semana é o contador do próprio jogo." : null}
          </p>
        </>
      )}
    </GameWindow>
  );
}

async function KindTabs({ searchParams }: { searchParams: Promise<Query> }) {
  const s = parse(await searchParams);
  return (
    <Tabs
      label="Ranking"
      tabs={[
        { href: url(s, { kind: "nivel" }), label: "Nível", active: s.kind === "nivel" },
        { href: url(s, { kind: "pvp" }), label: "PvP", active: s.kind === "pvp" },
        { href: url(s, { kind: "clas" }), label: "Clãs", active: s.kind === "clas" },
      ]}
    />
  );
}

export default function RankingPage({ searchParams }: PageProps<"/ranking">) {
  const sp = searchParams as Promise<Query>;
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="font-display text-2xl text-gold-bright">Ranking</h1>
        <Link href="/personagens" className="text-sm text-arcane hover:underline">
          Buscar personagem
        </Link>
      </div>
      <Suspense fallback={null}>
        <div className="mb-4">
          <KindTabs searchParams={sp} />
        </div>
      </Suspense>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <RankingBody searchParams={sp} />
      </Suspense>
    </div>
  );
}
