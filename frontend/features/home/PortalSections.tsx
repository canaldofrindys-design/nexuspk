import Image from "next/image";
import Link from "next/link";
import { ArtLink, kitArt, rankEmblems } from "@/components/ui/ArtLink";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { classes } from "./classes";
import { formatNumber } from "./format";
import type { HomeSnapshot } from "./types";

/*
 * Blocos da home na ordem da prancha "home" do kit visual (05_pranchas_e_mockups/home.png).
 * Nenhum número vem da arte: status, jogadores e taxas saem da API (/api/v1/home).
 */

/** Título de painel da prancha: plaquinha dourada, ícone e texto em caixa alta centralizados. */
export function Plate({ icon, title, subtitle }: { icon: KitIconName; title: string; subtitle?: string }) {
  return (
    <div className="nx-plate flex items-center justify-center gap-2.5 px-3 py-2">
      <KitIcon name={icon} size={40} />
      <div className="text-center leading-tight">
        <h2 className="font-display text-lg font-bold tracking-wide text-gold-bright uppercase [text-shadow:0_2px_4px_rgb(0_0_0/0.8)]">{title}</h2>
        {subtitle ? <p className="font-display text-[11px] tracking-[0.2em] text-gold-bright/80 uppercase">{subtitle}</p> : null}
      </div>
    </div>
  );
}

/** Coluna à direita do banner: Download do jogo, Cadastre-se agora e Começar a jogar. */
export function HomeActions() {
  return (
    <nav aria-label="Comece aqui" className="mx-auto grid w-full max-w-[330px] gap-2 lg:mx-0 lg:max-w-none lg:content-center">
      <ArtLink href="/download" art={kitArt.botoes.downloadHome} label="Download do jogo" sizes="330px" priority />
      <ArtLink href="/criar-conta" art={kitArt.botoes.cadastroHome} label="Cadastre-se agora: criar conta" sizes="330px" priority />
      <ArtLink href="/servidor" art={kitArt.botoes.jogarHome} label="Começar a jogar: conheça o servidor" sizes="330px" priority />
    </nav>
  );
}

const rankingLinks = [
  { href: "/ranking", label: "Top Level", icon: "rankingGeral" },
  { href: "/ranking?tipo=pvp", label: "Top PvP", icon: "pvp" },
  { href: "/ranking?tipo=clas", label: "Top Clã", icon: "guilda" },
  { href: "/ranking?periodo=mensal", label: "Top Mensal", icon: "eventos" },
  { href: "/ranking?periodo=semanal", label: "Top Semanal", icon: "rankingSemanal" },
  { href: "/ranking?periodo=diario", label: "Top Diário", icon: "rankingDiario" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName }[];

/** Painel "Ranking / Top geral": atalhos para cada ranking que o portal publica. */
export function RankingLinks() {
  return (
    <section aria-label="Rankings" className="game-window p-2">
      <Plate icon="trofeu" title="Ranking" subtitle="Top geral" />
      <ul className="mt-2 px-1">
        {rankingLinks.map((r) => (
          <li key={r.href} className="nx-row last:border-0">
            <Link href={r.href} className="flex items-center gap-3 px-2 py-2 text-[15px] text-silver transition-colors hover:text-gold-bright">
              <KitIcon name={r.icon} size={26} />
              {r.label}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** Faixa de emblemas de patente da prancha (arte do kit). Leva ao ranking. */
export function RankEmblems() {
  return (
    <Link href="/ranking" className="art-link block" aria-label="Patentes do NexusPK: ver o ranking">
      <ul className="grid grid-cols-4 items-end justify-items-center gap-x-1 gap-y-2 sm:grid-cols-7 lg:gap-x-0">
        {rankEmblems.map((e) => (
          <li key={e.name}>
            <Image src={e.src} alt="" width={e.width} height={e.height} unoptimized className="nx-emblem h-auto w-[84px] object-contain sm:w-full sm:max-w-[124px]" />
          </li>
        ))}
      </ul>
    </Link>
  );
}

const availability = {
  online: { text: "Online", className: "text-data-ok", dot: "bg-data-ok shadow-[0_0_12px_#7ee08a]" },
  offline: { text: "Offline", className: "text-data-bad", dot: "bg-data-bad shadow-[0_0_12px_#ff5a4a]" },
  partial: { text: "Parcial", className: "text-data-name", dot: "bg-data-name" },
  unknown: { text: "Verificando", className: "text-silver/70", dot: "bg-silver/50" },
} as const;

function rateIcon(label: string): KitIconName {
  const l = label.toLowerCase();
  if (l.includes("exp")) return "trofeu";
  if (l.includes("gold") || l.includes("ouro")) return "loja";
  if (l.includes("drop")) return "recompensas";
  return "mapa";
}

/** Painel "Status do servidor": estado, jogadores online e as taxas cadastradas no painel. */
export function ServerStatusPanel({ home }: { home: HomeSnapshot | null }) {
  const s = availability[home?.status.availability ?? "unknown"];
  const rates = Object.entries(home?.server.rates ?? {}).slice(0, 4);
  const players = home?.status.playersOnline;
  return (
    <section aria-labelledby="status-title" className="game-window p-2">
      <div className="nx-plate px-3 py-1.5 text-center">
        <h2 id="status-title" className="font-display text-sm font-bold tracking-wide text-silver uppercase">
          Status do servidor
        </h2>
      </div>
      <p className={`nx-pill-online mt-2 flex items-center justify-center gap-2 py-1 font-display text-lg font-bold uppercase ${s.className}`}>
        <span aria-hidden className={`inline-block size-3.5 rounded-full ${s.dot}`} />
        {home ? s.text : "API indisponível"}
      </p>
      <dl className="mt-1 px-2">
        {players != null ? (
          <div className="nx-row flex items-center gap-3 py-1.5">
            <KitIcon name="online" size={26} />
            <dt className="flex-1 text-silver/90">Jogadores online</dt>
            <dd className="tabular text-data-info">{formatNumber(players)}</dd>
          </div>
        ) : null}
        {rates.map(([label, value]) => (
          <div key={label} className="nx-row flex items-center gap-3 py-1.5 last:border-0">
            <KitIcon name={rateIcon(label)} size={26} />
            <dt className="flex-1 text-silver/90">{label}</dt>
            <dd className="tabular font-semibold text-silver">{value}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-1 px-2 pb-1 text-center text-xs">
        <Link href="/status" className="text-arcane hover:underline">
          Login, canais e manutenções
        </Link>
      </p>
    </section>
  );
}

/** As seis classes da prancha (Lutador, Mecânico, Arqueira, Pikeman, Assassina, Sacerdotisa). */
const featured = [1, 2, 3, 4, 9, 8];

export function ClassesStrip() {
  const list = featured.map((code) => classes.find((c) => c.code === code)!);
  return (
    <section aria-labelledby="classes-title" className="game-window flex h-full flex-col px-2 pt-1.5 pb-2">
      <h2 id="classes-title" className="text-center font-display text-sm font-bold tracking-wide text-gold-bright uppercase">
        <Link href="/classes" className="hover:underline">
          Classes do jogo
        </Link>
      </h2>
      <ul className="mt-1.5 grid flex-1 grid-cols-6 gap-1.5">
        {list.map((c) => (
          <li key={c.code}>
            <Link href={`/classes#${c.code}`} className="group flex flex-col items-center gap-1">
              <span className="block overflow-hidden rounded-md border border-bronze shadow-[0_6px_14px_-6px_rgb(0_0_0/0.9)] group-hover:border-gold-bright group-hover:shadow-[0_0_14px_rgb(53_184_255/0.45)]">
                <Image src={c.icon} alt="" width={84} height={84} className="size-full object-cover" />
              </span>
              <span className="max-w-full truncate text-[10px] font-semibold tracking-tighter text-silver group-hover:text-gold-bright md:text-[9px] xl:text-[10px]">{c.name}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

export function BossesCard() {
  return (
    <ArtLink href="/eventos?categoria=boss" art={kitArt.banners.bossesGrande} label="Bosses mundiais: eventos de boss" sizes="(min-width: 1024px) 260px, 50vw" />
  );
}

export function ClanWarCard() {
  return <ArtLink href="/ranking?tipo=clas" art={kitArt.banners.guerraDeClas} label="Guerra de clãs: ranking de clãs" sizes="(min-width: 1024px) 220px, 50vw" />;
}
