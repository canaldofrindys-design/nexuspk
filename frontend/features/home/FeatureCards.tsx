import { ArtLink, kitArt } from "@/components/ui/ArtLink";

/**
 * Faixas do topo da prancha "portal" do kit: cinco banners largos e, logo abaixo, os botões
 * (Download, Criar conta, Guia do jogo, Suporte). Cada peça leva à página real correspondente.
 * No celular os banners viram uma faixa com rolagem lateral e os botões, duas colunas.
 */
const banners = [
  {
    href: "/eventos?categoria=especial",
    art: kitArt.banners.eventosEspeciais,
    label: "Eventos especiais: agenda",
  },
  {
    href: "/eventos?categoria=boss",
    art: kitArt.banners.bossMundiais,
    label: "Bosses mundiais: eventos de boss",
  },
  {
    href: "/ranking?tipo=pvp",
    art: kitArt.banners.pvpLiberado,
    label: "PvP: ranking de batalha",
  },
  {
    href: "/ranking?periodo=semanal",
    art: kitArt.banners.rankingSemanal,
    label: "Ranking semanal: níveis ganhos na semana",
  },
  {
    href: "/ranking?periodo=diario",
    art: kitArt.banners.rankingDiario,
    label: "Ranking diário: níveis ganhos hoje",
  },
] as const;

/*
 * A prancha tem também "Doar / Cash": fica de fora até o portal ter a página de doação,
 * para nenhum botão levar a uma página vazia.
 */
const buttons = [
  {
    href: "/instalacao",
    art: kitArt.botoes.downloadDoJogo,
    label: "Download do jogo e instalação",
  },
  { href: "/criar-conta", art: kitArt.botoes.criarConta, label: "Criar conta" },
  { href: "/guias", art: kitArt.botoes.guia, label: "Guia do jogo" },
  { href: "/suporte", art: kitArt.botoes.suporte, label: "Suporte" },
] as const;

export function FeatureCards() {
  return (
    <nav aria-label="Destaques do servidor" className="-mx-4 lg:mx-0">
      <ul className="flex snap-x snap-mandatory scroll-px-4 gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] lg:grid lg:grid-cols-5 lg:gap-3 lg:overflow-visible lg:px-0 lg:pb-0">
        {banners.map((c, i) => (
          <li
            key={c.href}
            data-reveal="zoom"
            style={{ "--reveal-delay": `${i * 60}ms` } as React.CSSProperties}
            className="aspect-[300/108] w-[64%] shrink-0 snap-start sm:w-[40%] lg:w-auto"
          >
            {/* Mesma caixa para os cinco: os recortes têm larguras diferentes e, esticados pela largura, ficavam com alturas diferentes. */}
            <ArtLink
              href={c.href}
              art={c.art}
              label={c.label}
              sizes="(min-width: 1024px) 20vw, 64vw"
              priority={i < 3}
              className="flex h-full items-center justify-center"
              imgClassName="h-full w-auto max-w-full object-contain"
            />
          </li>
        ))}
      </ul>
    </nav>
  );
}

export function ActionButtons() {
  return (
    <nav aria-label="Comece aqui">
      {/* Todos na mesma altura (como na prancha), cada um na sua largura natural: nada é ampliado. */}
      <ul className="flex flex-wrap items-center justify-center gap-x-3 gap-y-2 md:justify-between md:gap-x-4">
        {buttons.map((b, i) => (
          <li
            key={b.href}
            data-reveal="up"
            style={{ "--reveal-delay": `${i * 70}ms` } as React.CSSProperties}
          >
            <ArtLink
              href={b.href}
              art={b.art}
              label={b.label}
              sizes="352px"
              imgClassName="h-[40px] w-auto sm:h-[52px] lg:h-[60px]"
            />
          </li>
        ))}
      </ul>
    </nav>
  );
}
