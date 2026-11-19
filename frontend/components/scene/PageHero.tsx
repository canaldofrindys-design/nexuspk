import type { ReactNode } from "react";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { SceneImage } from "./SceneImage";
import type { SceneKey } from "./scenes";

type Props = {
  scene: SceneKey;
  icon: KitIconName;
  title: string;
  /** Linha de apoio abaixo do título. */
  lead?: ReactNode;
  /** Texto curto em versalete acima do título. */
  eyebrow?: string;
  /** Links ou botões à direita (ou abaixo, no celular). */
  aside?: ReactNode;
};

/**
 * Abertura das páginas internas: cenário pintado em parallax com aproximação lenta, névoa,
 * véu noturno e o título em ouro. A página inteira passa a usar o mesmo cenário como fundo
 * (data-scene), que depois troca conforme os blocos marcados mais abaixo.
 */
export function PageHero({ scene, icon, title, lead, eyebrow, aside }: Props) {
  return (
    <section data-scene={scene} data-mouse-scope className="page-hero px-scope relative isolate overflow-hidden border-b border-bronze/50">
      <div aria-hidden className="px-layer absolute inset-x-0 -inset-y-[22%] -z-20" style={{ "--px-depth": "14%" } as React.CSSProperties}>
        <div className="hero-mouse size-full" style={{ "--mouse-depth": "-10px" } as React.CSSProperties}>
          <SceneImage scene={scene} sizes="100vw" priority className="page-hero__img size-full object-cover" />
        </div>
      </div>
      <div aria-hidden className="hero-fog hero-fog--front -z-10 opacity-30!" />
      <div aria-hidden className="page-hero__shade absolute inset-0 -z-10" />
      <div className="hero-fade mx-auto flex max-w-7xl flex-col gap-4 px-4 pt-12 pb-9 sm:pt-16 sm:pb-12 md:flex-row md:items-end md:justify-between">
        <div className="max-w-2xl">
          {eyebrow ? (
            <p className="hero-in font-display text-xs tracking-[0.28em] text-gold-bright/90 uppercase" style={{ "--in-delay": "40ms" } as React.CSSProperties}>
              {eyebrow}
            </p>
          ) : null}
          <h1 className="hero-in mt-1 flex items-center gap-3" style={{ "--in-delay": "120ms" } as React.CSSProperties}>
            <KitIcon name={icon} size={64} className="size-12 sm:size-16" />
            <span className="text-gilded font-display text-4xl font-bold sm:text-5xl">{title}</span>
          </h1>
          {lead ? (
            <div className="hero-in mt-3 max-w-xl leading-relaxed text-silver/90 [text-shadow:0_2px_6px_rgb(0_0_0/0.9)]" style={{ "--in-delay": "220ms" } as React.CSSProperties}>
              {lead}
            </div>
          ) : null}
        </div>
        {aside ? (
          <div className="hero-in shrink-0" style={{ "--in-delay": "320ms" } as React.CSSProperties}>
            {aside}
          </div>
        ) : null}
      </div>
    </section>
  );
}
