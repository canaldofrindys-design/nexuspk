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
}: {
  href: string;
  art: Art;
  label: string;
  className?: string;
  sizes?: string;
  priority?: boolean;
}) {
  return (
    <Link href={href} className={`art-link group block ${className}`} title={label}>
      <Image
        src={art.src}
        alt={label}
        width={art.width}
        height={art.height}
        sizes={sizes}
        priority={priority}
        unoptimized
        className="h-auto w-full object-contain"
      />
    </Link>
  );
}

/** Peças do kit (public/kit), com o tamanho real de cada recorte para preservar a proporção. */
export const kitArt = {
  cards: {
    pvp: { src: "/kit/cards/pvp-card.webp", width: 243, height: 128 },
    boss: { src: "/kit/cards/boss-card.webp", width: 248, height: 128 },
    eventos: { src: "/kit/cards/eventos-card.webp", width: 236, height: 128 },
    rankingGeral: { src: "/kit/cards/ranking-geral-card.webp", width: 242, height: 128 },
    rankingDiario: { src: "/kit/cards/ranking-diario-card.webp", width: 241, height: 128 },
    rankingSemanal: { src: "/kit/cards/ranking-semanal-card.webp", width: 246, height: 128 },
  },
  banners: {
    eventosEspeciais: { src: "/kit/cards/eventos-especiais.webp", width: 298, height: 113 },
    bossMundiais: { src: "/kit/cards/boss-mundiais.webp", width: 292, height: 113 },
    pvpLiberado: { src: "/kit/cards/pvp-liberado.webp", width: 281, height: 113 },
    rankingSemanal: { src: "/kit/cards/ranking-semanal.webp", width: 289, height: 113 },
    rankingDiario: { src: "/kit/cards/ranking-diario.webp", width: 284, height: 113 },
    destaque: { src: "/kit/cards/banner-destaque.webp", width: 1042, height: 212 },
    cabecalho: { src: "/kit/cards/cabecalho-nexuspk.webp", width: 1536, height: 182 },
  },
  botoes: {
    downloadDoJogo: { src: "/kit/botoes/download-do-jogo.webp", width: 357, height: 84 },
    criarConta: { src: "/kit/botoes/criar-conta.webp", width: 276, height: 84 },
    doacoes: { src: "/kit/botoes/doacoes-cash.webp", width: 256, height: 84 },
    guia: { src: "/kit/botoes/guia-do-jogo.webp", width: 272, height: 84 },
    suporte: { src: "/kit/botoes/suporte.webp", width: 240, height: 84 },
    downloadHome: { src: "/kit/botoes/download-home.webp", width: 333, height: 73 },
    cadastroHome: { src: "/kit/botoes/cadastro-home.webp", width: 333, height: 68 },
    jogarHome: { src: "/kit/botoes/jogar-agora-home.webp", width: 333, height: 69 },
  },
} as const satisfies Record<string, Record<string, Art>>;
