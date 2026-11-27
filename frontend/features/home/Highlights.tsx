import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";
import { ClassIcon } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { KitIcon, PodiumMedal } from "@/components/ui/KitIcon";
import { ClanEmblem } from "./ClanEmblem";
import { formatNumber } from "./format";
import { PanelTitle } from "./PortalSections";
import { nextBlessCastle } from "./eventSchedule";
import { serverEvents } from "./serverEvents";
import type { HomeClanLeader, HomeSnapshot } from "./types";

/*
 * Destaques da home: ranking de BP, líder do Bless Castle, líder do SoD e os eventos fixos do servidor.
 * Tudo que é número ou nome sai da API (/api/v1/home); sem fonte, o bloco diz que está indisponível.
 */

/** Painel "Ranking de BP" (coluna esquerda da prancha): os cinco com mais pontos de batalha. */
export function BattlePointsPanel({ home }: { home: HomeSnapshot | null }) {
  const rows = home?.topBattlePoints ?? [];
  const available = home?.battlePointsAvailable ?? false;
  return (
    <section aria-labelledby="ranking-bp-t" className="nx-panel">
      <PanelTitle icon="pvp" title="Ranking de BP" id="ranking-bp-t" />
      {!available ? (
        <p className="px-4 py-4 text-center text-sm text-silver/70">Ranking indisponível no momento.</p>
      ) : rows.length === 0 ? (
        <p className="px-4 py-4 text-center text-sm text-silver/70">Ninguém pontuou em batalha ainda.</p>
      ) : (
        <ol className="nx-panel__list" aria-label="Cinco personagens com mais pontos de batalha">
          {rows.map((p) => (
            <li key={p.name}>
              <Link href={`/personagens/${encodeURIComponent(p.name)}`} className="nx-panel__row nx-panel__row--data" title={`${p.className}, ${formatNumber(p.battlePoints)} BP`}>
                <span className="grid w-6 shrink-0 place-items-center tabular text-silver/70">
                  {p.position <= 3 ? <PodiumMedal position={p.position} height={22} /> : p.position}
                </span>
                <ClassIcon code={p.classCode} name={p.className} size={22} />
                <span className="min-w-0 flex-1 truncate text-data-name">{p.name}</span>
                <span className="tabular shrink-0 text-data-info">{formatNumber(p.battlePoints)}</span>
              </Link>
            </li>
          ))}
        </ol>
      )}
      <p className="px-3 pt-1 pb-2.5 text-center text-xs">
        <Link href="/ranking?tipo=pvp" className="text-arcane hover:underline">
          Ranking PvP completo
        </Link>
      </p>
    </section>
  );
}

const brtDate = new Intl.DateTimeFormat("pt-BR", { timeZone: "America/Sao_Paulo", day: "2-digit", month: "2-digit", year: "numeric" });

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-t border-steel/50 py-1 first:border-t-0">
      <dt className="text-silver/60">{label}</dt>
      <dd className="min-w-0 text-right text-silver">{children}</dd>
    </div>
  );
}

function CharacterLine({ name, classCode, className, level }: { name: string; classCode: number; className: string; level: number | null }) {
  return (
    <Link href={`/personagens/${encodeURIComponent(name)}`} className="inline-flex max-w-full items-center gap-1.5 hover:text-gold" title={className || undefined}>
      {classCode ? <ClassIcon code={classCode} name={className} size={18} /> : null}
      <span className="truncate font-semibold text-data-name">{name}</span>
      {level != null ? <span className="tabular shrink-0 text-xs text-silver/70">Nv {level}</span> : null}
    </Link>
  );
}

function LeaderBody({ leader, empty, sod }: { leader: HomeClanLeader | null | undefined; empty: string; sod: boolean }) {
  if (!leader?.available) {
    return <p className="py-2 text-center text-sm text-silver/70">Indisponível no momento.</p>;
  }
  if (!leader.clan) {
    return <p className="py-2 text-center text-sm text-silver/80">{empty}</p>;
  }
  const c = leader.clan;
  return (
    <>
      <div className="flex items-center gap-3">
        <ClanEmblem iconId={c.iconId ?? 0} name={c.name} size={44} />
        <div className="min-w-0">
          <p className="text-[11px] tracking-wider text-silver/60 uppercase">Clã</p>
          <Link href={`/clas/${encodeURIComponent(c.name)}`} className="text-gilded block font-display text-base leading-tight font-bold [overflow-wrap:anywhere] hover:underline">
            {c.name}
          </Link>
        </div>
      </div>
      <dl className="mt-2 text-xs">
        {c.leader ? (
          <Fact label="Líder">
            <CharacterLine {...c.leader} />
          </Fact>
        ) : null}
        <Fact label="Membros">
          <span className="tabular">{formatNumber(c.members)}</span>
        </Fact>
        {c.points > 0 ? (
          <Fact label="Pontos no SoD">
            <span className="tabular text-data-info">{formatNumber(c.points)}</span>
            {c.sodPosition && !sod ? <span className="text-silver/60"> ({c.sodPosition}º)</span> : null}
          </Fact>
        ) : null}
        {(c.wins ?? 0) + (c.losses ?? 0) > 0 ? (
          <Fact label="Vitórias / derrotas">
            <span className="tabular">
              <span className="text-data-ok">{formatNumber(c.wins ?? 0)}</span> / <span className="text-data-bad">{formatNumber(c.losses ?? 0)}</span>
            </span>
          </Fact>
        ) : null}
        {c.createdUtc ? (
          <Fact label="Fundado em">
            <span className="tabular">{brtDate.format(new Date(c.createdUtc))}</span>
          </Fact>
        ) : null}
      </dl>
    </>
  );
}

function SodTopPlayer({ leader }: { leader: HomeClanLeader | null | undefined }) {
  const p = leader?.available ? leader.topPlayer : null;
  if (!p) return null;
  return (
    <div className="mt-2 rounded-md border border-gold/30 bg-night/40 px-2.5 py-2 text-xs">
      <p className="mb-1 flex items-center gap-1.5 text-[11px] tracking-wider text-gold-bright uppercase">
        <PodiumMedal position={1} height={16} />
        Melhor do mês
      </p>
      <CharacterLine name={p.name} classCode={p.classCode} className={p.className} level={p.level} />
      <p className="mt-0.5 text-silver/70">
        <span className="tabular text-data-info">{formatNumber(p.points)} pontos</span>
        {p.clan ? <> · clã {p.clan}</> : null}
      </p>
    </div>
  );
}

function LeaderPanel({
  id,
  title,
  leader,
  empty,
  sod,
  note,
}: {
  id: string;
  title: string;
  leader: HomeClanLeader | null | undefined;
  empty: string;
  sod: boolean;
  note: string;
}) {
  return (
    <section aria-labelledby={id} className="nx-panel nx-panel--fill">
      <PanelTitle icon={sod ? "trofeu" : "guildasBandeira"} title={title} id={id} />
      <div className="flex flex-1 flex-col justify-center px-3 py-3">
        <LeaderBody leader={leader} empty={empty} sod={sod} />
        {sod ? <SodTopPlayer leader={leader} /> : null}
        <p className="mt-2 text-center text-[11px] text-silver/55">{note}</p>
        {sod ? (
          <Link href="/ranking/sod" className="mt-1 text-center text-xs text-arcane hover:underline">
            Ver o ranking do SoD
          </Link>
        ) : null}
      </div>
    </section>
  );
}

const brt = new Intl.DateTimeFormat("pt-BR", { timeZone: "America/Sao_Paulo", weekday: "long", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/** Coluna direita da prancha: líder do Bless Castle e do SoD, com as bandeiras e os orbes. */
export function LeadersColumn({ home }: { home: HomeSnapshot | null }) {
  return (
    <div className="flex flex-col justify-between gap-4">
      <LeaderPanel
        id="lider-bc-t"
        title="Líder do Bless Castle"
        leader={home?.blessCastle}
        empty="O castelo está sem dono."
        sod={false}
        note={`Próxima guerra: ${brt.format(nextBlessCastle(home?.schedule))}`}
      />
      <LeaderPanel
        id="lider-sod-t"
        title="Líder do SoD"
        leader={home?.sodLeader}
        empty="Nenhum clã pontuou no SoD ainda."
        sod
        note="Clã com mais pontos no Survival of the Death"
      />
      <Link
        href="/ranking?tipo=clas"
        aria-label="Clãs: ver o ranking de clãs"
        className="art-link grid grid-cols-5 justify-items-center gap-1 px-1"
      >
        {[0, 1, 2, 3, 4].map((i) => (
          <Image
            key={i}
            src={`/kit/prancha/bandeira-${i}.webp`}
            alt=""
            width={56}
            height={80}
            unoptimized
            className="h-auto w-full max-w-[52px]"
          />
        ))}
      </Link>
    </div>
  );
}

/** Janela "Eventos do servidor": os eventos fixos do jogo, com o próximo Bless Castle. */
export function ServerEventsWindow() {
  return (
    <GameWindow title="Eventos do servidor" icon="eventos">
      <ul className="divide-y divide-steel/60">
        {serverEvents.map((e) => (
          <li key={e.id} className="flex items-start gap-3 py-2 first:pt-0 last:pb-0">
            <KitIcon name={e.icon} size={30} className="mt-0.5" />
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-baseline justify-between gap-x-3">
                <Link href={e.href} className="font-semibold text-silver hover:text-gold hover:underline">
                  {e.name}
                </Link>
                <span className="text-xs text-data-info">{e.when}</span>
              </p>
              <p className="text-xs text-silver/70">{e.what}</p>
            </div>
          </li>
        ))}
      </ul>
      <p className="mt-3 text-xs text-silver/50">
        Horários de Brasília. Datas extras saem na agenda.{" "}
        <Link href="/manual#pvp" className="text-arcane hover:underline">
          Como funcionam
        </Link>
      </p>
    </GameWindow>
  );
}
