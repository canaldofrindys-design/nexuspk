"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { safeImage } from "@/features/content/labels";
import { safeHref } from "@/features/site/links";
import type { PublicBanner } from "@/features/site/types";

/**
 * Banners e destaques cadastrados no painel. Vários banners giram a cada 7 s
 * (parado com o mouse em cima, com o foco dentro ou com "reduzir movimento" ligado).
 */
export function BannerCarousel({ banners }: { banners: PublicBanner[] }) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const count = banners.length;

  useEffect(() => {
    if (count < 2 || paused) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const timer = window.setInterval(() => setIndex((i) => (i + 1) % count), 7000);
    return () => window.clearInterval(timer);
  }, [count, paused]);

  if (count === 0) return null;
  const current = banners[Math.min(index, count - 1)];
  const image = safeImage(current.imageUrl);
  const href = safeHref(current.linkUrl);
  const external = href?.startsWith("https://") ?? false;

  return (
    <section
      aria-roledescription="carrossel"
      aria-label="Destaques"
      className="game-window relative overflow-hidden"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={() => setPaused(false)}
    >
      <div key={current.id} className="banner-slide relative flex min-h-44 flex-col justify-end p-4 sm:min-h-52 sm:p-5" aria-live="polite">
        {image ? (
          // eslint-disable-next-line @next/next/no-img-element -- imagem enviada pelo painel ou https externo
          <img src={image} alt="" className="absolute inset-0 size-full object-cover" />
        ) : null}
        <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-night via-night/70 to-night/10" />
        <div className="relative max-w-xl">
          <p className="text-[11px] tracking-[0.2em] text-gold uppercase">Destaque</p>
          <h2 className="mt-1 font-display text-xl text-gold-bright drop-shadow sm:text-2xl">{current.title}</h2>
          {current.subtitle ? <p className="mt-1 text-sm text-silver/90">{current.subtitle}</p> : null}
          {href ? (
            external ? (
              <a
                href={href}
                target="_blank"
                rel="noopener noreferrer"
                className="btn-glow mt-3 inline-flex items-center rounded-sm border border-gold-bright/60 bg-gradient-to-b from-gold-bright to-gold px-3.5 py-1.5 text-sm font-semibold text-night"
              >
                {current.linkLabel || "Saiba mais"}
              </a>
            ) : (
              <Link
                href={href}
                className="btn-glow mt-3 inline-flex items-center rounded-sm border border-gold-bright/60 bg-gradient-to-b from-gold-bright to-gold px-3.5 py-1.5 text-sm font-semibold text-night"
              >
                {current.linkLabel || "Saiba mais"}
              </Link>
            )
          ) : null}
        </div>
      </div>
      {count > 1 ? (
        <div className="absolute right-3 bottom-3 flex gap-1.5">
          {banners.map((b, i) => (
            <button
              key={b.id}
              type="button"
              onClick={() => setIndex(i)}
              aria-label={`Mostrar destaque ${i + 1} de ${count}: ${b.title}`}
              aria-current={i === index}
              className={`size-2.5 rounded-full border border-gold/80 ${i === index ? "bg-gold-bright" : "bg-night/70 hover:bg-gold/50"}`}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}
