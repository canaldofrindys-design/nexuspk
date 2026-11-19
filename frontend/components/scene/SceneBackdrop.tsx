"use client";

import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { scenes, sceneFallback, sceneSrcSet, type SceneKey } from "./scenes";

/**
 * Fundo do portal que troca de cenário conforme a rolagem.
 *
 * Qualquer bloco com `data-scene="<cena>"` vira um trecho do fundo: quando o meio da tela entra
 * nele, o cenário dele aparece por trás de todo o site em um esmaecer lento, e o anterior some.
 * O fundo fica preso à tela, mais escuro que a arte original (véu noturno) e se desloca um pouco
 * mais devagar que a página (parallax). Páginas sem `data-scene` ficam só com o céu de estrelas.
 *
 * Desempenho: cada cena só é baixada quando o trecho dela chega a menos de duas telas de
 * distância; no celular vai sempre o arquivo de 1024px. Com "reduzir movimento", a troca é direta.
 */
function isScene(v: string | undefined): v is SceneKey {
  return !!v && v in scenes;
}

export function SceneBackdrop() {
  const pathname = usePathname();
  const [active, setActive] = useState<SceneKey | null>(null);
  const [loaded, setLoaded] = useState<SceneKey[]>([]);
  const [ready, setReady] = useState<SceneKey[]>([]);

  useEffect(() => {
    let frame = 0;
    let marks: HTMLElement[] = [];

    const collect = () => {
      marks = Array.from(document.querySelectorAll<HTMLElement>("main [data-scene]")).filter((el) => isScene(el.dataset.scene));
    };

    const update = () => {
      frame = 0;
      const vh = window.innerHeight;
      const line = vh * 0.5;
      let current: SceneKey | null = null;
      let firstBelow: SceneKey | null = null;
      const near: SceneKey[] = [];
      for (const el of marks) {
        const r = el.getBoundingClientRect();
        const key = el.dataset.scene as SceneKey;
        // O último bloco que cruza a linha do meio vence (blocos internos vêm depois dos externos).
        if (r.top <= line && r.bottom > line) current = key;
        if (!firstBelow && r.top > line) firstBelow = key;
        if (r.top < vh * 2 && r.bottom > -vh) near.push(key);
      }
      if (!marks.length) {
        setActive(null);
        return;
      }
      // Acima do primeiro trecho (topo da página), já mostra o primeiro cenário.
      if (!current && marks.length && marks[0].getBoundingClientRect().top > line) current = firstBelow;
      if (current) setActive(current);
      if (near.length) setLoaded((prev) => (near.every((k) => prev.includes(k)) ? prev : Array.from(new Set([...prev, ...near]))));
    };

    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };

    collect();
    schedule();

    // Partes da página chegam depois (streaming com Suspense): procura de novo quando o HTML muda.
    let pendingScan = 0;
    const mo = new MutationObserver(() => {
      if (pendingScan) return;
      pendingScan = window.setTimeout(() => {
        pendingScan = 0;
        collect();
        schedule();
      }, 120);
    });
    const main = document.querySelector("main");
    if (main) mo.observe(main, { childList: true, subtree: true });

    window.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", schedule);
    return () => {
      window.removeEventListener("scroll", schedule);
      window.removeEventListener("resize", schedule);
      mo.disconnect();
      if (frame) cancelAnimationFrame(frame);
      if (pendingScan) clearTimeout(pendingScan);
    };
  }, [pathname]);

  const markReady = (key: SceneKey) => setReady((prev) => (prev.includes(key) ? prev : [...prev, key]));

  return (
    <div aria-hidden className="scene-backdrop" data-active={active && ready.includes(active) ? "" : undefined}>
      <div className="scene-backdrop__track">
        {loaded.map((key) => (
          <picture key={key}>
            <source media="(max-width: 767px)" srcSet={sceneSrcSet(key, 1024)} sizes="100vw" />
            <source srcSet={sceneSrcSet(key)} sizes="100vw" />
            <img
              src={sceneFallback(key)}
              alt=""
              decoding="async"
              style={{ objectPosition: scenes[key].focus }}
              className="scene-backdrop__layer"
              data-on={key === active && ready.includes(key) ? "" : undefined}
              onLoad={() => markReady(key)}
              ref={(img) => {
                // Imagem que já estava no cache termina de carregar antes do React ligar o onLoad.
                if (img?.complete && img.naturalWidth) markReady(key);
              }}
            />
          </picture>
        ))}
      </div>
      <div className="scene-backdrop__veil" />
    </div>
  );
}
