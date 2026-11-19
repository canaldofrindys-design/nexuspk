import Image from "next/image";
import Link from "next/link";

export type Art = { src: string; width: number; height: number };

/**
 * Card, banner ou botão do kit visual (arte com texto embutido) usado como link.
 * O `label` vira o texto alternativo e o nome acessível: diz para onde o link leva,
 * não só o que está escrito na arte.
 */
export function ArtLink({
  href,
  art,
  label,
  className = "",
  sizes,
  priority = false,
  imgClassName = "h-auto w-full object-contain",
}: {
  href: string;
  art: Art;
  label: string;
  className?: string;
  sizes?: string;
  priority?: boolean;
  /** Classe da imagem: padrão ocupa a largura; use altura fixa para alinhar peças de tamanhos diferentes. */
  imgClassName?: string;
}) {
  return (
    <Link
      href={href}
      className={`art-link group block ${className}`}
      title={label}
    >
      <Image
        src={art.src}
        alt={label}
        width={art.width}
        height={art.height}
        sizes={sizes}
        priority={priority}
        unoptimized
        className={imgClassName}
      />
    </Link>
  );
}

/** Peças do kit (public/kit), com o tamanho real de cada recorte para preservar a proporção. */
export const kitArt = {
  cards: {
    pvp: { src: "/kit/cards/pvp-card.webp", width: 243, height: 115 },
    boss: { src: "/kit/cards/boss-card.webp", width: 244, height: 116 },
    eventos: { src: "/kit/cards/eventos-card.webp", width: 235, height: 116 },
    rankingGeral: {
      src: "/kit/cards/ranking-geral-card.webp",
      width: 241,
      height: 116,
    },
    rankingDiario: {
      src: "/kit/cards/ranking-diario-card.webp",
      width: 239,
      height: 116,
    },
    rankingSemanal: {
      src: "/kit/cards/ranking-semanal-card.webp",
      width: 242,
      height: 115,
    },
  },
  banners: {
    eventosEspeciais: {
      src: "/kit/cards/eventos-especiais.webp",
      width: 298,
      height: 105,
    },
    bossMundiais: {
      src: "/kit/cards/boss-mundiais.webp",
      width: 291,
      height: 107,
    },
    pvpLiberado: {
      src: "/kit/cards/pvp-liberado.webp",
      width: 280,
      height: 106,
    },
    rankingSemanal: {
      src: "/kit/cards/ranking-semanal.webp",
      width: 285,
      height: 107,
    },
    rankingDiario: {
      src: "/kit/cards/ranking-diario.webp",
      width: 273,
      height: 108,
    },
    destaque: {
      src: "/kit/cards/banner-destaque.webp",
      width: 1142,
      height: 212,
    },
    /** Recorte do centro para o celular (kit Web Ready): logo e cristal preservados. */
    destaqueMobile: {
      src: "/kit/cards/banner-destaque-mobile.webp",
      width: 560,
      height: 212,
    },
    cabecalho: {
      src: "/kit/cards/cabecalho-nexuspk.webp",
      width: 1536,
      height: 182,
    },
    cabecalhoMobile: {
      src: "/kit/cards/cabecalho-nexuspk-mobile.webp",
      width: 720,
      height: 182,
    },
    /** Recortes da prancha "home" (05_pranchas_e_mockups/home.png). */
    bossesGrande: {
      src: "/kit/cards/bosses-mundiais-grande.webp",
      width: 268,
      height: 124,
    },
    guerraDeClas: {
      src: "/kit/cards/guerra-de-clas.webp",
      width: 224,
      height: 124,
    },
  },
  botoes: {
    downloadDoJogo: {
      src: "/kit/botoes/download-do-jogo.webp",
      width: 352,
      height: 67,
    },
    criarConta: { src: "/kit/botoes/criar-conta.webp", width: 263, height: 66 },
    doacoes: { src: "/kit/botoes/doacoes-cash.webp", width: 251, height: 67 },
    guia: { src: "/kit/botoes/guia-do-jogo.webp", width: 269, height: 67 },
    suporte: { src: "/kit/botoes/suporte.webp", width: 229, height: 67 },
    downloadHome: {
      src: "/kit/botoes/download-home.webp",
      width: 331,
      height: 66,
    },
    cadastroHome: {
      src: "/kit/botoes/cadastro-home.webp",
      width: 331,
      height: 68,
    },
    jogarHome: {
      src: "/kit/botoes/jogar-agora-home.webp",
      width: 332,
      height: 69,
    },
  },
} as const satisfies Record<string, Record<string, Art>>;

/**
 * Peças recortadas da prancha "portal" (05_pranchas_e_mockups/portal.png) com recorte fino:
 * fundo do mockup removido (transparência real) e, nos tiles, a plaquinha limpa para o rótulo em HTML
 * (a arte trazia textos como "PvP em breve", que não são dados reais).
 */
export const pranchaArt = {
  tiles: {
    bosses: { src: "/kit/prancha/tile-bosses.webp", width: 170, height: 118 },
    eventos: { src: "/kit/prancha/tile-eventos.webp", width: 168, height: 117 },
    pvp: { src: "/kit/prancha/tile-pvp.webp", width: 167, height: 117 },
    castelo: { src: "/kit/prancha/tile-castelo.webp", width: 169, height: 118 },
    guerra: { src: "/kit/prancha/tile-guerra.webp", width: 167, height: 118 },
  },
  itens: [
    { key: "pocao-hp", name: "Poções de HP", width: 73, height: 70 },
    { key: "pocao-mp", name: "Poções de MP", width: 73, height: 70 },
    { key: "pergaminho", name: "Pergaminhos e missões", width: 72, height: 70 },
    { key: "cristal", name: "Cristais de invocação", width: 74, height: 72 },
    { key: "bigorna", name: "Aging", width: 73, height: 70 },
    { key: "martelo", name: "Ferreiro e mix", width: 74, height: 70 },
    { key: "sheltom-rubi", name: "Sheltoms", width: 74, height: 70 },
    { key: "sheltom-safira", name: "Force Orbs", width: 74, height: 70 },
    { key: "bau", name: "Recompensas", width: 63, height: 58 },
    { key: "chave", name: "Relíquias", width: 74, height: 70 },
    { key: "asas", name: "Montarias e skins", width: 74, height: 70 },
  ],
} as const;

/** Emblemas de patente (kit Web Ready: recorte com canal alfa, sem o fundo do mockup). Cada um já traz o nome na plaquinha. */
export const rankEmblems = [
  { name: "Bronze", src: "/kit/emblemas/bronze.webp", width: 105, height: 105 },
  { name: "Prata", src: "/kit/emblemas/prata.webp", width: 108, height: 105 },
  { name: "Ouro", src: "/kit/emblemas/ouro.webp", width: 101, height: 105 },
  {
    name: "Platina",
    src: "/kit/emblemas/platina.webp",
    width: 108,
    height: 105,
  },
  {
    name: "Diamante",
    src: "/kit/emblemas/diamante.webp",
    width: 120,
    height: 105,
  },
  { name: "Mestre", src: "/kit/emblemas/mestre.webp", width: 114, height: 105 },
  {
    name: "Grão-Mestre",
    src: "/kit/emblemas/grao-mestre.webp",
    width: 113,
    height: 105,
  },
] as const;
