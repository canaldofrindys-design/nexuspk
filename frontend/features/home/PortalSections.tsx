import Image from "next/image";
import Link from "next/link";
import { pranchaArt } from "@/components/ui/ArtLink";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { classes } from "./classes";
import { formatNumber } from "./format";
import type { HomeSnapshot } from "./types";

/*
 * Corpo da home na ordem da prancha "portal" do kit visual (05_pranchas_e_mockups/portal.png):
 * coluna esquerda com Ranking de BP e Status do servidor; centro com classes e a "Jornada do Herói"
 * (HeroJourney.tsx: drops, elos e cards ao vivo); coluna direita com os líderes do Bless Castle e do SoD (Highlights.tsx).
 * Nenhum número vem da arte: status, jogadores e taxas saem da API (/api/v1/home).
 */

/** Título de painel da prancha: faixa dourada com ícone e texto em caixa alta. */
export function PanelTitle({
  icon,
  title,
  id,
}: {
  icon?: KitIconName;
  title: string;
  id: string;
}) {
  return (
    <div className="nx-panel-title">
      <span aria-hidden className="nx-panel-title__gem" />
      {icon ? <KitIcon name={icon} size={30} /> : null}
      <h2
        id={id}
        className="font-display text-[15px] font-bold tracking-wide text-gold-bright uppercase [text-shadow:0_2px_4px_rgb(0_0_0/0.8)]"
      >
        {title}
      </h2>
    </div>
  );
}

const availability = {
  online: {
    text: "Servidor online",
    className: "text-data-ok",
    dot: "bg-data-ok shadow-[0_0_12px_#7ee08a]",
  },
  offline: {
    text: "Servidor offline",
    className: "text-data-bad",
    dot: "bg-data-bad shadow-[0_0_12px_#ff5a4a]",
  },
  partial: {
    text: "Parcial",
    className: "text-data-name",
    dot: "bg-data-name",
  },
  unknown: {
    text: "Verificando",
    className: "text-silver/70",
    dot: "bg-silver/50",
  },
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
          <span
            aria-hidden
            className={`ml-1.5 inline-block size-3.5 shrink-0 rounded-full ${s.dot}`}
          />
          <dt className="sr-only">Estado</dt>
          <dd className={`ml-1 font-semibold ${s.className}`}>
            {home ? s.text : "API indisponível"}
          </dd>
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
              <Image
                src={c.icon}
                alt=""
                width={84}
                height={84}
                className="aspect-square w-full object-cover"
              />
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
              <Image
                src={`/kit/prancha/item-${it.key}.webp`}
                alt={it.name}
                width={it.width}
                height={it.height}
                unoptimized
                className="h-auto w-full object-contain"
              />
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
