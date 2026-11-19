import { scenes, sceneFallback, sceneSrcSet, type SceneKey } from "./scenes";

type Props = {
  scene: SceneKey;
  /** Vazio quando a imagem é só cenário (a maioria dos casos). */
  alt?: string;
  sizes: string;
  className?: string;
  /** Imagem principal da página (abertura): carrega primeiro, sem esperar a rolagem. */
  priority?: boolean;
};

/**
 * Cenário em <picture>: no celular (até 767px) sempre o arquivo de 1024px, porque a tela de
 * densidade 3x pediria o maior arquivo sem ganho visível sob o véu escuro; do tablet em diante,
 * o navegador escolhe pelo `sizes`. Sem next/image porque os arquivos já saem otimizados em
 * public/art/cenas (o portal roda com imagens "unoptimized").
 */
export function SceneImage({ scene, alt = "", sizes, className = "", priority = false }: Props) {
  const s = scenes[scene];
  return (
    <picture>
      <source media="(max-width: 767px)" srcSet={sceneSrcSet(scene, 1024)} sizes="100vw" />
      <source srcSet={sceneSrcSet(scene)} sizes={sizes} />
      <img
        src={sceneFallback(scene)}
        alt={alt}
        width={s.width}
        height={s.height}
        loading={priority ? "eager" : "lazy"}
        fetchPriority={priority ? "high" : "auto"}
        decoding="async"
        draggable={false}
        style={{ objectPosition: s.focus }}
        className={className}
      />
    </picture>
  );
}
