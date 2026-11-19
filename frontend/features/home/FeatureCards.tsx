import { ArtLink, kitArt } from "@/components/ui/ArtLink";

/**
 * Faixa de cards do kit visual, logo abaixo da abertura (como na prancha "home" do kit).
 * Cada card leva à página real correspondente; nenhum mostra número ou status inventado.
 * No celular vira uma faixa com rolagem lateral; do notebook em diante, seis colunas.
 */
const cards = [
  { href: "/ranking?tipo=pvp", art: kitArt.cards.pvp, label: "PvP: ranking de batalha (BP e abates)" },
  { href: "/eventos?categoria=boss", art: kitArt.cards.boss, label: "Bosses mundiais: eventos de boss" },
  { href: "/eventos?categoria=especial", art: kitArt.cards.eventos, label: "Eventos especiais: agenda" },
  { href: "/ranking", art: kitArt.cards.rankingGeral, label: "Ranking geral por nível" },
  { href: "/ranking?periodo=diario", art: kitArt.cards.rankingDiario, label: "Ranking diário: níveis ganhos hoje" },
  { href: "/ranking?periodo=semanal", art: kitArt.cards.rankingSemanal, label: "Ranking semanal: níveis ganhos na semana" },
] as const;

export function FeatureCards() {
  return (
    <nav aria-label="Destaques do servidor" className="-mx-4 lg:mx-0">
      <ul className="flex snap-x snap-mandatory scroll-px-4 gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] lg:grid lg:grid-cols-6 lg:gap-3 lg:overflow-visible lg:px-0 lg:pb-0">
        {cards.map((c, i) => (
          <li
            key={c.href}
            data-reveal="zoom"
            style={{ "--reveal-delay": `${i * 60}ms` } as React.CSSProperties}
            className="w-[42%] shrink-0 snap-start sm:w-[30%] lg:w-auto"
          >
            <ArtLink href={c.href} art={c.art} label={c.label} sizes="(min-width: 1024px) 16vw, 42vw" />
          </li>
        ))}
      </ul>
    </nav>
  );
}
