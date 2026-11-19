import Image from "next/image";
import Link from "next/link";
import { pranchaArt, rankEmblems } from "@/components/ui/ArtLink";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { classes } from "./classes";
import { formatNumber } from "./format";
import type { HomeSnapshot } from "./types";

/*
 * Corpo da home na ordem da prancha "portal" do kit visual (05_pranchas_e_mockups/portal.png):
 * coluna esquerda com Ranking geral e Status do servidor; centro com classes, itens, patentes e os
 * cinco tiles; coluna direita com Rankings, bandeiras de clã e orbes.
 * Nenhum número vem da arte: status, jogadores e taxas saem da API (/api/v1/home).
 */

/** Título de painel da prancha: faixa dourada com ícone e texto em caixa alta. */
export function PanelTitle({ icon, title, id }: { icon?: KitIconName; title: string; id: string }) {
  return (
    <div className="nx-panel-title">
      <span aria-hidden className="nx-panel-title__gem" />
      {icon ? <KitIcon name={icon} size={30} /> : null}
      <h2 id={id} className="font-display text-[15px] font-bold tracking-wide text-gold-bright uppercase [text-shadow:0_2px_4px_rgb(0_0_0/0.8)]">
        {title}
      </h2>
    </div>
  );
}

const topLinks = [
  { href: "/ranking", label: "Top Level", icon: "rankingGeral" },
  { href: "/ranking?tipo=pvp", label: "Top Kill (PvP)", icon: "pvp" },
  { href: "/ranking?tipo=clas", label: "Top Clã", icon: "guildasBandeira" },
  { href: "/personagens", label: "Buscar personagem", icon: "personagem" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName }[];

/** Painel "Ranking geral" (coluna esquerda da prancha). */
export function RankingLinks() {
  return (
    <section aria-labelledby="ranking-geral-t" className="nx-panel">
      <PanelTitle icon="rankingGeral" title="Ranking geral" id="ranking-geral-t" />
      <ul className="nx-panel__list">
        {topLinks.map((r) => (
          <li key={r.href}>
            <Link href={r.href} className="nx-panel__row">
              <KitIcon name={r.icon} size={26} />
              {r.label}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

const periodLinks = [
  { href: "/ranking?periodo=diario", label: "Ranking diário", icon: "rankingDiario" },
  { href: "/ranking?periodo=semanal", label: "Ranking semanal", icon: "rankingSemanal" },
  { href: "/ranking?periodo=mensal", label: "Ranking mensal", icon: "eventos" },
  { href: "/ranking?tipo=clas", label: "Ranking de clãs", icon: "guilda" },
  { href: "/ranking", label: "Hall da fama", icon: "trofeu" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName }[];

/** Painel "Rankings" (coluna direita), com as bandeiras de clã e os orbes da prancha. */
export function RankingPeriods() {
  return (
    <div className="flex flex-col gap-4">
      <section aria-labelledby="rankings-t" className="nx-panel">
        <PanelTitle icon="rankingGeral" title="Rankings" id="rankings-t" />
        <ul className="nx-panel__list">
          {periodLinks.map((r) => (
            <li key={r.label}>
              <Link href={r.href} className="nx-panel__row">
                <KitIcon name={r.icon} size={26} />
                {r.label}
              </Link>
            </li>
          ))}
        </ul>
      </section>
      <Link href="/ranking?tipo=clas" aria-label="Clãs: ver o ranking de clãs" className="art-link grid grid-cols-5 justify-items-center gap-1 px-1">
        {[0, 1, 2, 3, 4].map((i) => (
          <Image key={i} src={`/kit/prancha/bandeira-${i}.webp`} alt="" width={56} height={80} unoptimized className="h-auto w-full max-w-[52px]" />
        ))}
      </Link>
      <div aria-hidden className="grid grid-cols-5 justify-items-center gap-1 px-1">
        {[0, 1, 2, 3, 4].map((i) => (
          <Image key={i} src={`/kit/prancha/orbe-${i}.webp`} alt="" width={62} height={62} unoptimized className="nx-orb h-auto w-full max-w-[50px]" />
        ))}
      </div>
    </div>
  );
}

const availability = {
  online: { text: "Servidor online", className: "text-data-ok", dot: "bg-data-ok shadow-[0_0_12px_#7ee08a]" },
  offline: { text: "Servidor offline", className: "text-data-bad", dot: "bg-data-bad shadow-[0_0_12px_#ff5a4a]" },
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
    <section aria-labelledby="status-title" className="nx-panel">
      <PanelTitle title="Status do servidor" id="status-title" />
      <dl className="nx-panel__list">
        <div className="nx-panel__row">
          <span aria-hidden className={`ml-1.5 inline-block size-3.5 shrink-0 rounded-full ${s.dot}`} />
          <dt className="sr-only">Estado</dt>
          <dd className={`ml-1 font-semibold ${s.className}`}>{home ? s.text : "API indisponível"}</dd>
        </div>
        {players != null ? (
          <div className="nx-panel__row">
            <KitIcon name="online" size={26} />
            <dt className="flex-1">Jogadores online</dt>
            <dd className="tabular text-data-info">{formatNumber(players)}</dd>
          </div>
        ) : null}
        {rates.map(([label, value]) => (
          <div key={label} className="nx-panel__row">
            <KitIcon name={rateIcon(label)} size={26} />
            <dt className="flex-1">{label}</dt>
            <dd className="tabular font-semibold text-silver">{value}</dd>
          </div>
        ))}
      </dl>
      <p className="px-3 pt-1 pb-2.5 text-center text-xs">
        <Link href="/status" className="text-arcane hover:underline">
          Login, canais e manutenções
        </Link>
      </p>
    </section>
  );
}

/** As sete classes da prancha (Lutador, Mecânico, Arqueira, Pikeman, Atalanta, Cavaleiro, Mago). */
const featured = [1, 2, 3, 4, 5, 6, 7];

export function ClassesStrip() {
  const list = featured.map((code) => classes.find((c) => c.code === code)!);
  return (
    <nav aria-label="Classes do jogo">
      <ul className="grid grid-cols-4 gap-2 sm:grid-cols-7">
        {list.map((c) => (
          <li key={c.code}>
            <Link href={`/classes#${c.code}`} className="nx-portrait group">
              <Image src={c.icon} alt="" width={84} height={84} className="aspect-square w-full object-cover" />
              <span className="nx-portrait__name">{c.name}</span>
            </Link>
          </li>
        ))}
        <li className="sm:hidden">
          <Link href="/classes" className="nx-portrait nx-portrait--more">
            <span className="font-display text-2xl text-gold-bright">+3</span>
            <span className="nx-portrait__name">Todas</span>
          </Link>
        </li>
      </ul>
    </nav>
  );
}

/** Itens da prancha em espaços de inventário. Cada um leva ao capítulo do manual ou guia que explica o sistema. */
const itemLinks: Record<string, string> = {
  "pocao-hp": "/manual#evolucao",
  "pocao-mp": "/manual#evolucao",
  pergaminho: "/guias/missoes-amanda",
  cristal: "/manual#itens",
  bigorna: "/guias/aging",
  martelo: "/manual#aprimoramento",
  "sheltom-rubi": "/guias/aging#tabela",
  "sheltom-safira": "/manual#aprimoramento",
  bau: "/eventos",
  chave: "/manual#itens",
  asas: "/manual#itens",
};

export function ItemSlots() {
  return (
    <nav aria-label="Sistemas do jogo">
      <ul className="grid grid-cols-6 gap-1.5 sm:grid-cols-11">
        {pranchaArt.itens.map((it) => (
          <li key={it.key}>
            <Link href={itemLinks[it.key]} title={it.name} className="nx-slot">
              <Image src={`/kit/prancha/item-${it.key}.webp`} alt={it.name} width={it.width} height={it.height} unoptimized className="h-auto w-full object-contain" />
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/** Patentes (medalhas do kit Web Ready com canal alfa). Levam ao ranking. */
export function RankEmblems() {
  return (
    <Link href="/ranking" className="art-link block" aria-label="Patentes do NexusPK: ver o ranking">
      <ul className="grid grid-cols-4 items-end justify-items-center gap-x-1 gap-y-2 sm:grid-cols-7">
        {rankEmblems.map((e) => (
          <li key={e.name}>
            <Image src={e.src} alt="" width={e.width} height={e.height} unoptimized className="h-[72px] w-auto object-contain sm:h-[88px] xl:h-[96px]" />
          </li>
        ))}
      </ul>
    </Link>
  );
}

/** Os cinco tiles do pé da prancha. O rótulo é HTML sobre a plaquinha limpa da arte. */
const tiles = [
  { href: "/eventos?categoria=boss", art: pranchaArt.tiles.bosses, label: "Bosses" },
  { href: "/eventos", art: pranchaArt.tiles.eventos, label: "Eventos" },
  { href: "/manual#pvp", art: pranchaArt.tiles.pvp, label: "Arena PvP" },
  { href: "/manual#pvp", art: pranchaArt.tiles.castelo, label: "Bless Castle" },
  { href: "/ranking?tipo=clas", art: pranchaArt.tiles.guerra, label: "Clãs" },
] as const;

export function PortalTiles() {
  return (
    <nav aria-label="Atividades">
      <ul className="grid grid-cols-2 gap-2 sm:grid-cols-5">
        {tiles.map((t, i) => (
          <li key={t.label} className={i === 4 ? "col-span-2 mx-auto w-1/2 sm:col-span-1 sm:w-auto" : undefined}>
            <Link href={t.href} className="nx-tile art-link">
              <Image src={t.art.src} alt="" width={t.art.width} height={t.art.height} unoptimized className="h-auto w-full" />
              <span className="nx-tile__label">{t.label}</span>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
