import Image from "next/image";
import Link from "next/link";
import { pranchaArt } from "@/components/ui/ArtLink";
import { ClassIcon } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { KitIcon, PodiumMedal } from "@/components/ui/KitIcon";
import { formatNumber } from "./format";
import { PanelTitle } from "./PortalSections";
import { nextBlessCastle, serverEvents } from "./serverEvents";
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

function LeaderBody({ leader, empty, points }: { leader: HomeClanLeader | null | undefined; empty: string; points: boolean }) {
  if (!leader?.available) {
    return <p className="text-sm text-silver/70">Indisponível no momento.</p>;
  }
  if (!leader.clan) {
    return <p className="text-sm text-silver/80">{empty}</p>;
  }
  const c = leader.clan;
  return (
    <>
      <p className="text-gilded font-display text-xl leading-tight font-bold break-words">{c.name}</p>
      <p className="mt-1 text-xs text-silver/80">
        {formatNumber(c.members)} {c.members === 1 ? "membro" : "membros"}
        {points ? (
          <>
            {" · "}
            <span className="tabular text-data-info">{formatNumber(c.points)} pontos</span>
          </>
        ) : null}
      </p>
    </>
  );
}

function LeaderPanel({
  id,
  title,
  art,
  leader,
  empty,
  points,
  note,
}: {
  id: string;
  title: string;
  art: { src: string; width: number; height: number };
  leader: HomeClanLeader | null | undefined;
  empty: string;
  points: boolean;
  note: string;
}) {
  return (
    <section aria-labelledby={id} className="nx-panel nx-panel--fill">
      <PanelTitle icon="guildasBandeira" title={title} id={id} />
      <div className="flex flex-1 flex-col items-center justify-center gap-2 px-4 py-3 text-center">
        <Image src={art.src} alt="" width={art.width} height={art.height} unoptimized className="h-auto w-24 rounded-md border border-bronze/60" />
        <div>
          <LeaderBody leader={leader} empty={empty} points={points} />
        </div>
        <p className="text-xs text-silver/60">{note}</p>
      </div>
    </section>
  );
}

const brt = new Intl.DateTimeFormat("pt-BR", { timeZone: "America/Sao_Paulo", day: "2-digit", month: "2-digit" });

/** Coluna direita da prancha: líder do Bless Castle e do SoD, com as bandeiras e os orbes. */
export function LeadersColumn({ home }: { home: HomeSnapshot | null }) {
  return (
    <div className="flex flex-col justify-between gap-4">
      <LeaderPanel
        id="lider-bc-t"
        title="Líder do Bless Castle"
        art={pranchaArt.tiles.castelo}
        leader={home?.blessCastle}
        empty="O castelo está sem dono."
        points={false}
        note={`Próxima guerra: domingo, ${brt.format(nextBlessCastle())}, 16:00`}
      />
      <LeaderPanel
        id="lider-sod-t"
        title="Líder do SoD"
        art={pranchaArt.tiles.pvp}
        leader={home?.sodLeader}
        empty="Nenhum clã pontuou no SoD ainda."
        points
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
