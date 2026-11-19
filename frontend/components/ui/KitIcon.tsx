import Image from "next/image";

/**
 * Ícones do kit visual do NexusPK (public/kit/icones, versão "-tight" do kit Web Ready, margem segura de 12px, WebP 160px).
 * Decorativos por padrão: o texto ao lado já diz o que é. Passe `label` quando o ícone estiver sozinho.
 */
export const kitIcons = {
  inicio: "inicio-castelo",
  personagem: "personagem-armadura",
  rankingGeral: "ranking-geral-coroa",
  rankingDiario: "ranking-diario",
  rankingSemanal: "ranking-semanal",
  pvp: "pvp",
  boss: "boss-mundial",
  guilda: "guilda",
  eventos: "eventos",
  recompensas: "recompensas-bau",
  download: "download",
  noticias: "noticias",
  guia: "guia-do-jogo",
  trofeu: "trofeu",
  loja: "loja-moedas",
  conta: "conta-login",
  guildasBandeira: "guildas-bandeira",
  mapa: "mapa-missoes",
  classes: "classes",
  online: "jogadores-online",
} as const;

export type KitIconName = keyof typeof kitIcons;

export function kitIconSrc(name: KitIconName) {
  return `/kit/icones/${kitIcons[name]}.webp`;
}

export function KitIcon({ name, size = 24, label, className = "" }: { name: KitIconName; size?: number; label?: string; className?: string }) {
  return (
    <Image
      src={kitIconSrc(name)}
      alt={label ?? ""}
      aria-hidden={label ? undefined : true}
      width={size}
      height={size}
      unoptimized
      className={`kit-icon shrink-0 object-contain ${className}`}
    />
  );
}

/** Medalhas do pódio (recorte com canal alfa do kit Web Ready): 1º ouro, 2º prata, 3º bronze. */
const podium = {
  1: { src: "/kit/emblemas/ouro.webp", w: 101, label: "1º lugar" },
  2: { src: "/kit/emblemas/prata.webp", w: 108, label: "2º lugar" },
  3: { src: "/kit/emblemas/bronze.webp", w: 105, label: "3º lugar" },
} as const;

export function PodiumMedal({ position, height = 26 }: { position: number; height?: number }) {
  const m = podium[position as 1 | 2 | 3];
  if (!m) return null;
  return (
    <Image
      src={m.src}
      alt={m.label}
      width={Math.round((m.w / 105) * height)}
      height={height}
      unoptimized
      className="inline-block object-contain align-middle"
    />
  );
}
