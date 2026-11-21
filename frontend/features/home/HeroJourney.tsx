import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";
import { pranchaArt, rankEmblems } from "@/components/ui/ArtLink";
import { Countdown } from "./Countdown";
import { formatDateTime, formatNumber } from "./format";
import { ItemSlots } from "./PortalSections";
import { RareDropsStrip, type DropView } from "./RareDropsStrip";
import { nextBlessCastle } from "./serverEvents";
import type { HomeEventItem, HomeSnapshot } from "./types";

/*
 * "Jornada do Herói": o centro da prancha deixa de ser só decoração e mostra o servidor vivo.
 * 1) Drops raros da semana (anunciados pela equipe no painel, em Banners);
 * 2) escada de elos PvP pelo ranking de BP, com quantos estão em cada elo e o melhor de cada um;
 * 3) os cinco cards com informação ao vivo: próximo boss, evento de agora, líder da arena,
 *    dono do Bless Castle e o maior clã.
 * Sem dado, cada parte volta ao visual de antes com uma linha curta dizendo o porquê.
 */

function RowTitle({ id, children, aside }: { id: string; children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="nx-row-title">
      <h3 id={id}>{children}</h3>
      {aside ? <span className="nx-row-title__aside">{aside}</span> : null}
    </div>
  );
}

const dropSlots = pranchaArt.itens.length;

export function RareDropsRow({ home }: { home: HomeSnapshot | null }) {
  const drops: DropView[] = (home?.rareDrops ?? []).slice(0, dropSlots).flatMap((d) => {
    const art = pranchaArt.itens.find((it) => it.key === d.icon);
    if (!d.code && !art) return [];
    return [
      {
        kind: "drop" as const,
        item: d.item,
        code: d.code ?? null,
        icon: art?.key ?? null,
        width: art?.width ?? 0,
        height: art?.height ?? 0,
        player: d.player,
        boss: d.boss,
        when: formatDateTime(d.droppedUtc),
      },
    ];
  });
  const showcase: DropView[] = (home?.showcase ?? []).slice(0, dropSlots - drops.length).map((s) => ({
    kind: "showcase" as const,
    item: s.name,
    code: s.code,
    icon: null,
    width: 0,
    height: 0,
    player: null,
    boss: s.boss,
    when: null,
  }));
  const slots = [...drops, ...showcase];
  return (
    <section aria-labelledby="drops-t">
      <RowTitle id="drops-t" aside={drops.length ? `${drops.length} esta semana` : undefined}>
        Drops raros da semana
      </RowTitle>
      {slots.length ? (
        <>
          <RareDropsStrip drops={slots} slots={dropSlots} />
          {drops.length === 0 ? <p className="nx-row-note">Nenhum drop raro nesta semana ainda. Estes itens caem dos bosses do servidor.</p> : null}
        </>
      ) : (
        <>
          <ItemSlots />
          <p className="nx-row-note">Nenhum drop raro nesta semana. Toque num item para ver o guia dele.</p>
        </>
      )}
    </section>
  );
}

const pct = (share: number) => `${Math.round(share * 100)}%`;

export function PvpLadderRow({ home }: { home: HomeSnapshot | null }) {
  const ladder = home?.pvpTiers ?? null;
  const byKey = new Map((ladder?.tiers ?? []).map((t) => [t.key, t]));
  const keyOf = (name: string) =>
    name
      .normalize("NFD")
      .replace(/[̀-ͯ]/g, "")
      .toLowerCase()
      .replace(/\s+/g, "-");
  const gm = byKey.get("grao-mestre")?.top ?? null;
  return (
    <section aria-labelledby="elos-t">
      <RowTitle
        id="elos-t"
        aside={
          gm ? (
            <>
              Grão-Mestre atual:{" "}
              <Link href={`/personagens/${encodeURIComponent(gm.name)}`} className="text-gold-bright hover:underline">
                {gm.name}
              </Link>
            </>
          ) : undefined
        }
      >
        Elos PvP
      </RowTitle>
      <ul className="grid grid-cols-4 items-start justify-items-center gap-x-1 gap-y-3 sm:grid-cols-7">
        {rankEmblems.map((e) => {
          const tier = byKey.get(keyOf(e.name));
          const isGm = e.name === "Grão-Mestre";
          const hint = tier
            ? `${e.name}: ${isGm ? "o" : "até o"} ${pct(tier.topShare)} do topo do ranking de BP${tier.minBattlePoints != null ? `, a partir de ${formatNumber(tier.minBattlePoints)} BP` : ""}`
            : e.name;
          return (
            <li key={e.name} className={`nx-tier ${isGm && tier?.top ? "nx-tier--gm" : ""}`}>
              <Link href="/ranking?tipo=pvp" className="art-link flex flex-col items-center" title={hint} aria-label={`${hint}. Ver o ranking PvP`}>
                <Image
                  src={e.src}
                  alt=""
                  width={e.width}
                  height={e.height}
                  unoptimized
                  className="h-[64px] w-auto object-contain sm:h-[80px] xl:h-[96px]"
                />
              </Link>
              {tier ? (
                <>
                  <span className="nx-tier__count tabular">
                    {formatNumber(tier.players)} {tier.players === 1 ? "jogador" : "jogadores"}
                  </span>
                  {tier.top ? (
                    <Link href={`/personagens/${encodeURIComponent(tier.top.name)}`} className="nx-tier__top" title={`Melhor do elo: ${tier.top.name}, ${formatNumber(tier.top.battlePoints)} BP`}>
                      {tier.top.name}
                    </Link>
                  ) : (
                    <span className="nx-tier__top nx-tier__top--empty">vago</span>
                  )}
                </>
              ) : null}
            </li>
          );
        })}
      </ul>
      <p className="nx-row-note">
        {!ladder
          ? "Elos indisponíveis no momento."
          : ladder.total === 0
            ? "Ninguém pontuou em batalha ainda: o primeiro BP já vira Grão-Mestre."
            : `Elo pela posição no ranking de BP entre ${formatNumber(ladder.total)} guerreiros: Grão-Mestre é o 1% do topo.`}
      </p>
    </section>
  );
}

type TileInfo = { main: ReactNode; sub?: ReactNode };

function bossInfo(active: HomeEventItem[], upcoming: HomeEventItem[]): TileInfo {
  const now = active.find((e) => e.category === "boss");
  if (now) return { main: now.name, sub: <span className="text-data-ok">Acontecendo agora</span> };
  const next = upcoming.find((e) => e.category === "boss");
  if (next) return { main: next.name, sub: <Countdown to={next.startsAtUtc} prefix="Nasce em " /> };
  return { main: "Próximo boss", sub: "Conforme a agenda" };
}

function eventInfo(active: HomeEventItem[], upcoming: HomeEventItem[]): TileInfo {
  const now = active[0];
  if (now) return { main: now.name, sub: now.endsAtUtc ? <Countdown to={now.endsAtUtc} prefix="Acaba em " /> : <span className="text-data-ok">Agora</span> };
  const next = upcoming[0];
  if (next) return { main: next.name, sub: <Countdown to={next.startsAtUtc} prefix="Começa em " /> };
  return { main: "Sem evento agora", sub: "Veja a agenda" };
}

export function LiveTilesRow({ home }: { home: HomeSnapshot | null }) {
  const active = home?.activeEvents ?? [];
  const upcoming = home?.upcomingEvents ?? [];
  const leader = home?.topBattlePoints?.[0] ?? null;
  const castle = home?.blessCastle;
  const biggest = home?.biggestClan;
  const war = nextBlessCastle().toISOString();

  const tiles: { href: string; art: (typeof pranchaArt.tiles)[keyof typeof pranchaArt.tiles]; label: string; info: TileInfo }[] = [
    { href: "/eventos?categoria=boss", art: pranchaArt.tiles.bosses, label: "Bosses", info: bossInfo(active, upcoming) },
    { href: "/eventos", art: pranchaArt.tiles.eventos, label: "Eventos", info: eventInfo(active, upcoming) },
    {
      href: "/ranking?tipo=pvp",
      art: pranchaArt.tiles.pvp,
      label: "Arena PvP",
      info: leader
        ? { main: <>Líder: {leader.name}</>, sub: `${formatNumber(leader.battlePoints)} BP` }
        : { main: home?.battlePointsAvailable ? "Arena sem líder" : "Líder da arena", sub: home?.battlePointsAvailable ? "Ninguém pontuou ainda" : "Sem dados agora" },
    },
    {
      href: "/manual#pvp",
      art: pranchaArt.tiles.castelo,
      label: "Bless Castle",
      info: {
        main: castle?.clan ? <>Dono: {castle.clan.name}</> : castle?.available ? "Castelo sem dono" : "Dono do castelo",
        sub: <Countdown to={war} prefix="Guerra em " />,
      },
    },
    {
      href: "/ranking?tipo=clas",
      art: pranchaArt.tiles.guerra,
      label: "Clãs",
      info: biggest?.clan
        ? { main: <>Maior: {biggest.clan.name}</>, sub: `${formatNumber(biggest.clan.members)} membros` }
        : { main: "Maior clã", sub: biggest?.available ? "Nenhum clã ainda" : "Sem dados agora" },
    },
  ];

  return (
    <nav aria-label="Atividades ao vivo">
      <ul className="grid grid-cols-2 gap-2 sm:grid-cols-5">
        {tiles.map((t, i) => (
          <li key={t.label} className={i === 4 ? "col-span-2 mx-auto w-1/2 sm:col-span-1 sm:w-auto" : undefined}>
            <Link href={t.href} className="nx-tile nx-tile--live art-link">
              <span className="relative block">
                <Image src={t.art.src} alt="" width={t.art.width} height={t.art.height} unoptimized className="h-auto w-full" />
                <span className="nx-tile__label">{t.label}</span>
              </span>
              <span className="nx-tile__info">
                <span className="nx-tile__main">{t.info.main}</span>
                {t.info.sub ? <span className="nx-tile__sub">{t.info.sub}</span> : null}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
