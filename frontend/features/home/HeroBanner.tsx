"use client";

import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { SceneImage } from "@/components/scene/SceneImage";
import type { SceneKey } from "@/components/scene/scenes";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";

/**
 * Banner animado da abertura da home: três cenas que se alternam com esmaecer cruzado, cada uma
 * com aproximação lenta da câmera (Ken Burns), brilho de sol ou de lua, névoa correndo em duas
 * velocidades e partículas (brasas douradas no entardecer, vaga-lumes azuis à noite).
 *
 * - A troca é automática a cada 8 s e pausa com o mouse em cima, com o foco do teclado dentro,
 *   com a aba em segundo plano ou com o banner fora da tela.
 * - Com "reduzir movimento" não há troca automática nem partículas: as setas e os marcadores
 *   continuam funcionando.
 * - Só a primeira cena é baixada de imediato; as outras chegam depois que a página termina de
 *   carregar, para não disputar banda com o que aparece primeiro.
 */
type Slide = {
  scene: SceneKey;
  label: string;
  eyebrow: string;
  icon: KitIconName;
  title: string;
  text: string;
  primary: { href: string; label: string };
  secondary: { href: string; label: string };
  mood: "sun" | "moon" | "day";
};

const slides: Slide[] = [
  {
    scene: "vale-dourado",
    label: "Bem-vindo",
    eyebrow: "Servidor privado de Priston Tale",
    icon: "inicio",
    title: "O clássico renasce no NexusPK",
    text: "Dez classes, rankings atualizados ao vivo, agenda pública de eventos e uma equipe que responde pelo suporte. Crie sua conta, baixe o cliente e entre no vale ainda hoje.",
    primary: { href: "/criar-conta", label: "Criar conta grátis" },
    secondary: { href: "/download", label: "Baixar o jogo" },
    mood: "sun",
  },
  {
    scene: "luar",
    label: "Noites de guerra",
    eyebrow: "Eventos, bosses e PvP",
    icon: "pvp",
    title: "Quando a lua sobe, a guerra começa",
    text: "Bosses mundiais com horário marcado, arenas de PvP com ranking de abates e a guerra de clãs pelo topo. Veja a agenda, ative o lembrete e reúna o seu grupo antes da hora.",
    primary: { href: "/eventos", label: "Ver agenda de eventos" },
    secondary: { href: "/ranking?tipo=pvp", label: "Ranking PvP" },
    mood: "moon",
  },
  {
    scene: "jornada",
    label: "Sua jornada",
    eyebrow: "Do primeiro passo ao topo",
    icon: "mapa",
    title: "Toda lenda começa na estrada",
    text: "Escolha entre lutadores, magos, arqueiras e mais sete classes, suba de nível e dispute o ranking geral, diário e semanal. Cada personagem tem um perfil público com a sua trajetória.",
    primary: { href: "/classes", label: "Conhecer as classes" },
    secondary: { href: "/ranking", label: "Ver o ranking" },
    mood: "day",
  },
];

const INTERVAL_MS = 8000;

/** Partículas com posições fixas (mesmo HTML no servidor e no navegador). */
const particles = Array.from({ length: 22 }, (_, i) => ({
  x: (i * 37 + 11) % 100,
  delay: -((i * 1.7) % 12),
  dur: 9 + ((i * 5) % 8),
  size: 2 + ((i * 3) % 4),
  sway: ((i % 2 ? 1 : -1) * (12 + ((i * 7) % 26))),
}));

export function HeroBanner() {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [visible, setVisible] = useState(true);
  const [reduced, setReduced] = useState(false);
  const [loadRest, setLoadRest] = useState(false);
  const ref = useRef<HTMLElement>(null);

  const go = useCallback((next: number) => setIndex((next + slides.length) % slides.length), []);

  useEffect(() => {
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    const sync = () => setReduced(mq.matches);
    sync();
    mq.addEventListener("change", sync);

    const startRest = () => setLoadRest(true);
    let t = 0;
    if (document.readyState === "complete") t = window.setTimeout(startRest, 600);
    else window.addEventListener("load", () => (t = window.setTimeout(startRest, 600)), { once: true });

    const io = new IntersectionObserver(([e]) => setVisible(e.isIntersecting), { threshold: 0.25 });
    if (ref.current) io.observe(ref.current);
    const onVis = () => setVisible(document.visibilityState === "visible");
    document.addEventListener("visibilitychange", onVis);
    return () => {
      mq.removeEventListener("change", sync);
      clearTimeout(t);
      io.disconnect();
      document.removeEventListener("visibilitychange", onVis);
    };
  }, []);

  const running = !reduced && !paused && visible && loadRest;
  useEffect(() => {
    if (!running) return;
    const t = window.setTimeout(() => go(index + 1), INTERVAL_MS);
    return () => clearTimeout(t);
  }, [running, index, go]);

  const slide = slides[index];

  return (
    <section
      ref={ref}
      aria-roledescription="carrossel"
      aria-label="Destaques do NexusPK"
      data-scene="vale-dourado"
      data-mouse-scope
      data-mood={slide.mood}
      className="hero-banner px-scope relative isolate overflow-hidden border-b border-bronze/60"
      onPointerEnter={(e) => e.pointerType === "mouse" && setPaused(true)}
      onPointerLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setPaused(false);
      }}
      onKeyDown={(e) => {
        if (e.key === "ArrowRight") go(index + 1);
        if (e.key === "ArrowLeft") go(index - 1);
      }}
    >
      {/* Cenas empilhadas: a ativa aparece por cima com esmaecer e aproximação lenta. */}
      <div aria-hidden className="hero-banner__scene px-layer" style={{ "--px-depth": "12%" } as React.CSSProperties}>
        <div className="hero-mouse size-full" style={{ "--mouse-depth": "-14px" } as React.CSSProperties}>
          {slides.map((s, i) =>
            i === 0 || loadRest ? (
              <div key={s.scene} className="hero-banner__slide" data-on={i === index ? "" : undefined}>
                <SceneImage scene={s.scene} sizes="100vw" priority={i === 0} className="hero-banner__img" />
              </div>
            ) : null,
          )}
        </div>
      </div>

      <div aria-hidden className="hero-banner__glow" />
      <div aria-hidden className="hero-rays hero-banner__rays" />
      <div aria-hidden className="hero-fog hero-fog--back" />
      {!reduced ? (
        <div aria-hidden className="hero-banner__particles">
          {particles.map((p, i) => (
            <span
              key={i}
              style={
                {
                  "--x": `${p.x}%`,
                  "--delay": `${p.delay}s`,
                  "--dur": `${p.dur}s`,
                  "--size": `${p.size}px`,
                  "--sway": `${p.sway}px`,
                } as React.CSSProperties
              }
            />
          ))}
        </div>
      ) : null}
      <div aria-hidden className="hero-fog hero-fog--front" />
      <div aria-hidden className="hero-banner__shade" />

      <div className="relative mx-auto flex w-full max-w-7xl flex-1 flex-col justify-end px-4 pt-16 pb-6 lg:justify-center lg:pb-16">
        <div key={index} className="hero-fade max-w-2xl" aria-live={running ? "off" : "polite"}>
          <p className="hero-in flex items-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase sm:text-sm" style={{ "--in-delay": "60ms" } as React.CSSProperties}>
            <KitIcon name={slide.icon} size={34} />
            {slide.eyebrow}
          </p>
          <h2 className="hero-in text-gilded mt-3 font-display text-[2.1rem] leading-[1.08] font-bold sm:text-5xl lg:text-6xl" style={{ "--in-delay": "160ms" } as React.CSSProperties}>
            {slide.title}
          </h2>
          <p className="hero-in mt-4 max-w-xl text-[15px] leading-relaxed text-silver/90 [text-shadow:0_2px_8px_rgb(0_0_0/0.9)] sm:text-lg" style={{ "--in-delay": "280ms" } as React.CSSProperties}>
            {slide.text}
          </p>
          <div className="hero-in mt-6 flex flex-wrap gap-3" style={{ "--in-delay": "400ms" } as React.CSSProperties}>
            <Link href={slide.primary.href} className="nx-btn btn-glow btn-shine inline-flex items-center px-5 py-3 font-display text-sm font-bold tracking-wide uppercase sm:text-base">
              {slide.primary.label}
            </Link>
            <Link
              href={slide.secondary.href}
              className="inline-flex items-center rounded-[9px] border border-gold-bright/60 bg-night/50 px-5 py-3 font-display text-sm font-bold tracking-wide text-gold-bright uppercase backdrop-blur-sm transition-colors hover:bg-night/80 sm:text-base"
            >
              {slide.secondary.label}
            </Link>
          </div>
        </div>

        <div className="mt-7 flex items-center gap-3 lg:mt-12">
          <button type="button" onClick={() => go(index - 1)} className="hero-banner__arrow" aria-label="Cena anterior">
            <ChevronLeft className="size-5" aria-hidden />
          </button>
          <ol className="flex flex-1 gap-2 sm:max-w-md" aria-label="Escolher cena">
            {slides.map((s, i) => (
              <li key={s.scene} className="flex-1">
                <button
                  type="button"
                  onClick={() => go(i)}
                  aria-current={i === index ? "true" : undefined}
                  aria-label={`${i + 1} de ${slides.length}: ${s.label}`}
                  className="hero-banner__dot group w-full text-left"
                >
                  <span className="hero-banner__bar">
                    <span
                      key={i === index ? `${index}-${running}` : "idle"}
                      className="hero-banner__fill"
                      data-state={i === index ? (running ? "run" : "full") : i < index ? "done" : undefined}
                      style={{ "--dur": `${INTERVAL_MS}ms` } as React.CSSProperties}
                    />
                  </span>
                  <span className="mt-1.5 hidden truncate text-[11px] font-semibold tracking-wider text-silver/70 uppercase group-aria-[current]:text-gold-bright sm:block">{s.label}</span>
                </button>
              </li>
            ))}
          </ol>
          <button type="button" onClick={() => go(index + 1)} className="hero-banner__arrow" aria-label="Próxima cena">
            <ChevronRight className="size-5" aria-hidden />
          </button>
        </div>
      </div>

      <a href="#portal" className="scroll-cue absolute right-8 bottom-6 hidden flex-col items-center gap-2 text-[10px] tracking-[0.3em] text-gold-bright/80 uppercase lg:flex" aria-label="Rolar até o portal">
        Role
        <span aria-hidden className="block h-10 w-px overflow-hidden bg-gold-bright/20">
          <span className="scroll-cue__line block h-full w-full bg-gold-bright" />
        </span>
      </a>
    </section>
  );
}
