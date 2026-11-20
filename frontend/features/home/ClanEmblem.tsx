"use client";

import { useEffect, useRef, useState } from "react";
import { KitIcon } from "@/components/ui/KitIcon";

/**
 * Emblema do clã, o mesmo .bmp que o jogo baixa (/ClanContent/<número>.bmp, servido pela pasta do site antigo no IIS).
 * Sem número ou se a imagem não carregar, mostra a bandeira do kit.
 */
export function ClanEmblem({ iconId, name, size = 40 }: { iconId: number; name: string; size?: number }) {
  const [failed, setFailed] = useState(false);
  const img = useRef<HTMLImageElement>(null);
  // A imagem pode falhar antes da hidratação, quando o onError ainda não existe.
  useEffect(() => {
    const el = img.current;
    if (el && el.complete && el.naturalWidth === 0) setFailed(true);
  }, []);
  const frame = "shrink-0 rounded-md border border-gold/60 bg-night/70 shadow-[0_0_10px_rgb(0_0_0/0.7)]";
  if (!iconId || failed) {
    return (
      <span style={{ width: size, height: size }} className={`${frame} grid place-items-center`}>
        <KitIcon name="guildasBandeira" size={size - 8} label={`Emblema do clã ${name}`} />
      </span>
    );
  }
  return (
    // eslint-disable-next-line @next/next/no-img-element -- .bmp do jogo, fora do otimizador de imagens
    <img
      ref={img}
      src={`/ClanContent/${iconId}.bmp`}
      alt={`Emblema do clã ${name}`}
      width={size}
      height={size}
      onError={() => setFailed(true)}
      style={{ width: size, height: size, imageRendering: "pixelated" }}
      className={frame}
    />
  );
}
