"use client";

import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { SceneImage } from "@/components/scene/SceneImage";
import type { SceneKey } from "@/components/scene/scenes";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import type { ScheduledEvent } from "@/features/site/types";
import { featuredEvents, scheduleKinds, type FeaturedEvent } from "./eventSchedule";
import type { HomeEventItem } from "./types";

/**
 * Banner animado da abertura da home. Primeiro vêm os eventos principais (Bless Castle e os outros
 * horários fixos do painel, mais a agenda dos próximos sete dias), cada um com contagem regressiva
 * até o início, ou até o fim quando já está acontecendo. Depois, as três cenas de apresentação.
 * Cada cena tem esmaecer cruzado, aproximação lenta da câmera (Ken Burns), brilho de sol ou de lua,
 * névoa e partículas.
 *
 * - A troca é automática a cada 8 s e pausa com o mouse em cima, com o foco do teclado dentro,
 *   com a aba em segundo plano ou com o banner fora da tela.
 * - Com "reduzir movimento" não há troca automática, partículas nem o pulo dos números:
 *   a contagem continua certa e as setas, os marcadores e a lista de eventos funcionam.
 * - Do tablet grande em diante, a lateral lista os eventos com a contagem de cada um.
 * - Só a primeira cena é baixada de imediato; as outras chegam depois que a página termina de carregar.
 */
type Mood = "sun" | "moon" | "day";
type Story = {
  type: "story";
  key: string;
  scene: SceneKey;
  label: string;
  eyebrow: string;
  icon: KitIconName;
  title: string;
  text: string;
  primary: { href: string; label: string };
  secondary: { href: string; label: string };
  mood: Mood;
};
type EventSlide = { type: "event"; key: string; scene: SceneKey; label: string; icon: KitIconName; mood: Mood; event: FeaturedEvent };
type Slide = Story | EventSlide;

const stories: Story[] = [
  {
    type: "story",
    key: "boas-vindas",
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
    type: "story",
    key: "noites-de-guerra",
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
    type: "story",
    key: "jornada",
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
  sway: (i % 2 ? 1 : -1) * (12 + ((i * 7) % 26)),
}));

const startFormat = new Intl.DateTimeFormat("pt-BR", {
  timeZone: "America/Sao_Paulo",
  weekday: "long",
  day: "2-digit",
  month: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
});

function parts(ms: number) {
  const s = Math.max(0, Math.floor(ms / 1000));
  return { d: Math.floor(s / 86400), h: Math.floor((s % 86400) / 3600), m: Math.floor((s % 3600) / 60), s: s % 60 };
}
const pad = (n: number) => String(n).padStart(2, "0");

/** "2d 04:12:09" ou "04:12:09": a forma curta da lateral. */
function shortCountdown(ms: number) {
  const p = parts(ms);
  const clock = `${pad(p.h)}:${pad(p.m)}:${pad(p.s)}`;
  return p.d > 0 ? `${p.d}d ${clock}` : clock;
}

/** Quatro caixas (dias, horas, minutos, segundos). O número que muda dá um pulo curto. */
function BigCountdown({ ms, urgent }: { ms: number | null; urgent: boolean }) {
  const p = parts(ms ?? 0);
  const cells = [
    { v: p.d, label: p.d === 1 ? "dia" : "dias" },
    { v: p.h, label: "horas" },
    { v: p.m, label: "min" },
    { v: p.s, label: "seg" },
  ];
  return (
    <div aria-hidden className="hero-count" data-urgent={urgent ? "" : undefined}>
      {cells.map((c) => (
        <div key={c.label} className="hero-count__cell">
          <span key={c.v} className="hero-count__num tabular" suppressHydrationWarning>
            {ms === null ? "--" : pad(c.v)}
          </span>
          <span className="hero-count__label">{c.label}</span>
        </div>
      ))}
    </div>
  );
}

/**
 * `initialNow`: hora do servidor quando a página é montada sob demanda. Sem ela (casca estática da página),
 * a contagem aparece como "--" até o navegador assumir, um instante depois.
 */
export function HeroBanner({ schedule, agenda = [], initialNow }: { schedule: ScheduledEvent[]; agenda?: HomeEventItem[]; initialNow?: number }) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [visible, setVisible] = useState(true);
  const [reduced, setReduced] = useState(false);
  const [loadRest, setLoadRest] = useState(false);
  const [now, setNow] = useState<number | null>(initialNow ?? null);
  const ref = useRef<HTMLElement>(null);

  // A lista só muda quando um evento começa ou termina: recalcula por minuto, não por segundo.
  const minute = now === null ? 0 : Math.floor(now / 60_000);
  const events = useMemo(() => featuredEvents(schedule, agenda, minute * 60_000), [schedule, agenda, minute]);
  const slides: Slide[] = useMemo(
    () => [
      ...events.map<EventSlide>((event) => {
        const k = scheduleKinds[event.kind];
        return { type: "event", key: event.key, scene: k.scene, label: event.name, icon: k.icon, mood: k.mood, event };
      }),
      ...stories,
    ],
    [events],
  );
  const count = slides.length;
  const current = Math.min(index, count - 1);
  const go = useCallback((next: number) => setIndex((next + count) % count), [count]);

  useEffect(() => {
    const tick = () => setNow(Date.now());
    const first = window.setTimeout(tick, 0);
    const id = window.setInterval(tick, 1000);
    return () => {
      window.clearTimeout(first);
      window.clearInterval(id);
    };
  }, []);

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
    const t = window.setTimeout(() => go(current + 1), INTERVAL_MS);
    return () => clearTimeout(t);
  }, [running, current, go]);

  const slide = slides[current];
  // Uma camada por cena (dois eventos podem usar a mesma arte).
  const sceneKeys = [...new Set(slides.map((s) => s.scene))];

  return (
    <section
      ref={ref}
      aria-roledescription="carrossel"
      aria-label="Destaques e próximos eventos do NexusPK"
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
        if (e.key === "ArrowRight") go(current + 1);
        if (e.key === "ArrowLeft") go(current - 1);
      }}
    >
      {/* Cenas empilhadas: a ativa aparece por cima com esmaecer e aproximação lenta. */}
      <div aria-hidden className="hero-banner__scene px-layer" style={{ "--px-depth": "12%" } as React.CSSProperties}>
        <div className="hero-mouse size-full" style={{ "--mouse-depth": "-14px" } as React.CSSProperties}>
          {sceneKeys.map((scene, i) =>
            i === 0 || loadRest ? (
              <div key={scene} className="hero-banner__slide" data-on={scene === slide.scene ? "" : undefined}>
                <SceneImage scene={scene} sizes="100vw" priority={i === 0} className="hero-banner__img" />
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

      <div className="relative mx-auto flex w-full max-w-7xl flex-1 flex-col justify-end px-4 pt-16 pb-6 lg:justify-center lg:pb-14">
        <div className="grid items-center gap-8 lg:grid-cols-[minmax(0,1fr)_340px]">
          <div key={slide.key} className="hero-fade max-w-3xl" aria-live={running ? "off" : "polite"}>
            {slide.type === "event" ? <EventContent slide={slide} now={now} /> : <StoryContent slide={slide} />}
          </div>

          {events.length > 0 ? (
            <aside aria-label="Próximos eventos" className="hero-rail hidden lg:flex">
              <p className="flex items-center gap-2 font-display text-xs tracking-[0.24em] text-gold-bright uppercase">
                <KitIcon name="eventos" size={26} />
                Próximos eventos
              </p>
              <ol className="flex flex-col gap-2">
                {events.map((ev, i) => {
                  const live = now !== null && ev.start <= now;
                  const k = scheduleKinds[ev.kind];
                  return (
                    <li key={ev.key}>
                      <button
                        type="button"
                        onClick={() => go(i)}
                        aria-current={i === current ? "true" : undefined}
                        className="hero-rail__item"
                      >
                        <KitIcon name={k.icon} size={40} />
                        <span className="min-w-0 flex-1 text-left">
                          <span className="block truncate font-display text-sm font-bold text-gold-bright">{ev.name}</span>
                          <span className="block truncate text-xs text-silver/70">
                            {ev.when || (now === null ? "" : startFormat.format(ev.start))}
                          </span>
                        </span>
                        <span className="text-right">
                          {live ? (
                            <span className="hero-live-badge">Agora</span>
                          ) : (
                            <span className="tabular block font-display text-sm text-silver" suppressHydrationWarning>
                              {now === null ? "--:--:--" : shortCountdown(ev.start - now)}
                            </span>
                          )}
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ol>
              <Link href="/eventos" className="text-right text-xs font-semibold tracking-wider text-arcane uppercase hover:underline">
                Agenda completa
              </Link>
            </aside>
          ) : null}
        </div>

        <div className="mt-7 flex items-center gap-3 lg:mt-10">
          <button type="button" onClick={() => go(current - 1)} className="hero-banner__arrow" aria-label="Destaque anterior">
            <ChevronLeft className="size-5" aria-hidden />
          </button>
          <ol className="flex flex-1 gap-2 sm:max-w-2xl" aria-label="Escolher destaque">
            {slides.map((s, i) => (
              <li key={s.key} className="min-w-0 flex-1">
                <button
                  type="button"
                  onClick={() => go(i)}
                  aria-current={i === current ? "true" : undefined}
                  aria-label={`${i + 1} de ${count}: ${s.label}`}
                  className="hero-banner__dot group w-full text-left"
                >
                  <span className="hero-banner__bar">
                    <span
                      key={i === current ? `${current}-${running}` : "idle"}
                      className="hero-banner__fill"
                      data-state={i === current ? (running ? "run" : "full") : i < current ? "done" : undefined}
                      style={{ "--dur": `${INTERVAL_MS}ms` } as React.CSSProperties}
                    />
                  </span>
                  <span className="mt-1.5 hidden truncate text-[11px] font-semibold tracking-wider text-silver/70 uppercase group-aria-[current]:text-gold-bright sm:block">
                    {s.label}
                  </span>
                </button>
              </li>
            ))}
          </ol>
          <button type="button" onClick={() => go(current + 1)} className="hero-banner__arrow" aria-label="Próximo destaque">
            <ChevronRight className="size-5" aria-hidden />
          </button>
        </div>
      </div>

      <a
        href="#portal"
        className="scroll-cue absolute right-8 bottom-6 hidden flex-col items-center gap-2 text-[10px] tracking-[0.3em] text-gold-bright/80 uppercase xl:flex"
        aria-label="Rolar até o portal"
      >
        Role
        <span aria-hidden className="block h-10 w-px overflow-hidden bg-gold-bright/20">
          <span className="scroll-cue__line block h-full w-full bg-gold-bright" />
        </span>
      </a>
    </section>
  );
}

const delay = (ms: number) => ({ "--in-delay": `${ms}ms` }) as React.CSSProperties;
const secondaryBtn =
  "inline-flex items-center rounded-[9px] border border-gold-bright/60 bg-night/50 px-5 py-3 font-display text-sm font-bold tracking-wide text-gold-bright uppercase backdrop-blur-sm transition-colors hover:bg-night/80 sm:text-base";
const primaryBtn = "nx-btn btn-glow btn-shine inline-flex items-center px-5 py-3 font-display text-sm font-bold tracking-wide uppercase sm:text-base";

function StoryContent({ slide }: { slide: Story }) {
  return (
    <>
      <p className="hero-in flex items-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase sm:text-sm" style={delay(60)}>
        <KitIcon name={slide.icon} size={34} />
        {slide.eyebrow}
      </p>
      <h2 className="hero-in text-gilded mt-3 font-display text-[2.1rem] leading-[1.08] font-bold sm:text-5xl lg:text-7xl" style={delay(160)}>
        {slide.title}
      </h2>
      <p className="hero-in mt-4 max-w-xl text-[15px] leading-relaxed text-silver/90 [text-shadow:0_2px_8px_rgb(0_0_0/0.9)] sm:text-lg" style={delay(280)}>
        {slide.text}
      </p>
      <div className="hero-in mt-6 flex flex-wrap gap-3" style={delay(400)}>
        <Link href={slide.primary.href} className={primaryBtn}>
          {slide.primary.label}
        </Link>
        <Link href={slide.secondary.href} className={secondaryBtn}>
          {slide.secondary.label}
        </Link>
      </div>
    </>
  );
}

function EventContent({ slide, now }: { slide: EventSlide; now: number | null }) {
  const ev = slide.event;
  const live = now !== null && ev.start <= now;
  const target = live ? (ev.end ?? ev.start) : ev.start;
  const left = now === null ? null : target - now;
  const startText = now === null ? "" : startFormat.format(ev.start);
  return (
    <>
      <p className="hero-in flex flex-wrap items-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase sm:text-sm" style={delay(60)}>
        <KitIcon name={slide.icon} size={34} />
        {live ? <span className="hero-live-badge">Acontecendo agora</span> : "Próximo evento"}
        {ev.when ? <span className="tracking-[0.16em] text-silver/80">· {ev.when}</span> : null}
      </p>
      <h2 className="hero-in text-gilded mt-3 font-display text-[2.3rem] leading-[1.04] font-bold sm:text-6xl lg:text-7xl" style={delay(160)}>
        {ev.name}
      </h2>
      <p className="hero-in mt-3 max-w-xl text-[15px] leading-relaxed text-silver/90 [text-shadow:0_2px_8px_rgb(0_0_0/0.9)] sm:text-lg" style={delay(240)}>
        {ev.description}
      </p>

      <div className="hero-in mt-5" style={delay(320)}>
        <p className="mb-2 text-xs font-semibold tracking-[0.22em] text-silver/80 uppercase">
          {live ? (ev.end ? "Termina em" : "Começou") : "Começa em"}
        </p>
        {live && !ev.end ? null : <BigCountdown ms={left} urgent={!live && left !== null && left < 3600_000} />}
        {startText ? (
          <p className="sr-only">{live ? `${ev.name} está acontecendo agora.` : `${ev.name} começa ${startText}.`}</p>
        ) : null}
        <p aria-hidden className="mt-2 text-sm text-silver/70 first-letter:uppercase">
          {startText}
        </p>
      </div>

      <div className="hero-in mt-6 flex flex-wrap gap-3" style={delay(420)}>
        <Link href="/eventos" className={primaryBtn}>
          Ver agenda de eventos
        </Link>
        <Link href={ev.href} className={secondaryBtn}>
          {ev.hrefLabel}
        </Link>
      </div>
    </>
  );
}
