import Image from "next/image";
import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Countdown } from "@/features/content/Countdown";
import { eventLabel, newsLabel } from "@/features/content/labels";
import { formatDateTime, formatNumber } from "./format";
import { HeroEmbers } from "./HeroEmbers";
import type { HomeSnapshot } from "./types";

const availability = {
  online: { text: "Servidor online", dot: "bg-data-ok shadow-[0_0_10px_#7ee08a]" },
  offline: { text: "Servidor offline", dot: "bg-data-bad" },
  partial: { text: "Servidor parcialmente fora do ar", dot: "bg-data-name" },
  unknown: { text: "Status em verificação", dot: "bg-silver/50" },
} as const;

const eventTag = "border-amethyst/60 text-amethyst";

/** O fato do momento: evento em andamento, próximo evento ou última notícia. */
function Highlight({ home }: { home: HomeSnapshot | null }) {
  const now = new Date().toISOString();
  const live = home?.activeEvents[0];
  const next = home?.upcomingEvents[0];
  const news = home?.news[0];
  if (!live && !next && !news) return null;

  return (
    <div className="glass-panel mt-6 max-w-md p-4">
      {live ? (
        <>
          <p className="flex items-center gap-2 text-xs">
            <span className={`border px-1.5 ${eventTag}`}>{eventLabel(live.category)}</span>
            <span className="text-data-ok">Em andamento</span>
          </p>
          <h2 className="mt-1.5 font-display text-2xl text-data-name">{live.name}</h2>
          {live.endsAtUtc ? (
            <div className="mt-3">
              <Countdown target={live.endsAtUtc} label="Termina em" serverNow={now} boxes />
            </div>
          ) : null}
          <Link href={`/eventos/${live.slug}`} className="mt-3 inline-block text-sm text-arcane hover:underline">
            Ver regras do evento
          </Link>
        </>
      ) : next ? (
        <>
          <p className="flex items-center gap-2 text-xs">
            <span className={`border px-1.5 ${eventTag}`}>{eventLabel(next.category)}</span>
            <span className="tabular text-silver/70">{formatDateTime(next.startsAtUtc)}</span>
          </p>
          <h2 className="mt-1.5 font-display text-2xl text-data-name">{next.name}</h2>
          <div className="mt-3">
            <Countdown target={next.startsAtUtc} label="Começa em" serverNow={now} boxes />
          </div>
          <Link href={`/eventos/${next.slug}`} className="mt-3 inline-block text-sm text-arcane hover:underline">
            Ver regras do evento
          </Link>
        </>
      ) : news ? (
        <>
          <span className={`border px-1.5 text-xs ${newsLabel(news.category).tone}`}>{newsLabel(news.category).label}</span>
          <h2 className="mt-1.5 font-display text-2xl text-data-name">{news.title}</h2>
          {news.summary ? <p className="mt-1 line-clamp-2 text-sm text-silver/85">{news.summary}</p> : null}
          <Link href={`/noticias/${news.slug}`} className="mt-3 inline-block text-sm text-arcane hover:underline">
            Ler a notícia
          </Link>
        </>
      ) : null}
    </div>
  );
}

/**
 * Abertura épica da home, montada em camadas de profundidade: céu do castelo (fundo, mais lento),
 * raios e portal pulsando, névoa ao fundo, brasas subindo, névoa da frente, o texto e o herói em
 * primeiro plano (mais rápido e seguindo o mouse). O parallax vem do CSS (globals.css, .px-layer).
 * O herói aparece só a partir do tablet.
 */
export function HomeHero({ home }: { home: HomeSnapshot | null }) {
  const state = availability[home?.status.availability ?? "unknown"];
  const players = home?.status.playersOnline;

  return (
    <section
      aria-label="Destaque"
      data-mouse-scope
      className="hero-epic px-scope relative isolate flex flex-col overflow-hidden border-b border-gold/40"
    >
      <div aria-hidden className="hero-scene px-layer" style={{ "--px-depth": "14%" } as React.CSSProperties}>
        <div className="hero-mouse absolute inset-0" style={{ "--mouse-depth": "-10px" } as React.CSSProperties}>
          <Image src="/art/castelo.webp" alt="" fill priority sizes="(max-width: 768px) 280vw, 100vw" className="hero-kenburns object-cover object-[60%_40%]" />
        </div>
      </div>
      <div aria-hidden className="hero-rays" />
      <div aria-hidden className="hero-portal" />
      <div aria-hidden className="absolute inset-0 -z-20 overflow-hidden">
        <div className="hero-fog hero-fog--back" />
      </div>
      <HeroEmbers />
      <div aria-hidden className="absolute inset-0 -z-10 bg-gradient-to-r from-night via-night/70 to-transparent" />
      <div aria-hidden className="absolute inset-x-0 bottom-0 -z-10 h-56 bg-gradient-to-t from-night via-night/60 to-transparent" />
      <div aria-hidden className="absolute inset-x-0 top-0 -z-10 h-28 bg-gradient-to-b from-night/80 to-transparent" />
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10 shadow-[inset_0_0_180px_60px_rgb(0_0_0/0.75)]" />
      <div aria-hidden className="absolute inset-0 -z-[5] overflow-hidden">
        <div className="hero-fog hero-fog--front" />
      </div>

      <div className="relative mx-auto grid w-full max-w-7xl flex-1 items-end gap-4 px-4 pt-8 pb-16 md:grid-cols-[1fr_minmax(0,540px)] md:pt-12">
        <div className="hero-fade self-center">
          <h1 className="sr-only">NexusPK, servidor privado de Priston Tale</h1>
          <span className="logo-shine hero-in">
            <Image
              src="/brand/logo-nexuspk.webp"
              alt=""
              width={400}
              height={161}
              priority
              className="h-auto w-64 drop-shadow-[0_0_32px_rgb(95_178_255/0.55)] sm:w-[420px]"
            />
          </span>
          <p
            className="hero-in mt-3 flex items-center gap-3 font-display text-sm tracking-[0.3em] text-gold uppercase"
            style={{ "--in-delay": "120ms" } as React.CSSProperties}
          >
            <span aria-hidden className="h-px w-10 bg-gradient-to-r from-transparent to-gold" />
            Priston Tale
            <span aria-hidden className="h-px w-10 bg-gradient-to-l from-transparent to-gold" />
          </p>
          <p className="hero-in mt-3 max-w-lg text-lg text-silver" style={{ "--in-delay": "200ms" } as React.CSSProperties}>
            {home?.server.tagline || "Servidor privado de Priston Tale."}
          </p>

          <div className="hero-in" style={{ "--in-delay": "320ms" } as React.CSSProperties}>
            <Highlight home={home} />
          </div>

          <div className="hero-in mt-6 flex flex-wrap gap-3" style={{ "--in-delay": "440ms" } as React.CSSProperties}>
            <ButtonLink href="/criar-conta" size="lg" shine>
              Criar conta
            </ButtonLink>
            <ButtonLink href="/painel" size="lg" variant="ghost">
              Painel do jogador
            </ButtonLink>
          </div>

          <ul
            className="hero-in mt-6 flex flex-wrap gap-x-6 gap-y-1 text-sm text-silver/85"
            style={{ "--in-delay": "560ms" } as React.CSSProperties}
          >
            <li className="flex items-center gap-2">
              <span aria-hidden className={`inline-block size-2.5 rounded-full ${state.dot}`} />
              {state.text}
            </li>
            {players != null ? (
              <li className="tabular">
                <span className="text-data-info">{formatNumber(players)}</span> jogadores online
              </li>
            ) : null}
            <li>Horários de Brasília</li>
          </ul>
        </div>

        <div aria-hidden className="px-layer hidden self-end md:block" style={{ "--px-depth": "-9%" } as React.CSSProperties}>
          <div className="hero-mouse" style={{ "--mouse-depth": "22px" } as React.CSSProperties}>
            <div className="hero-in-right">
              <div className="hero-float relative">
                <span className="hero-aura" />
                <Image
                  src="/art/heroi-azul.webp"
                  alt=""
                  width={1318}
                  height={1100}
                  priority
                  sizes="(min-width: 768px) 540px, 0px"
                  className="h-auto w-full drop-shadow-[0_24px_40px_rgb(0_0_0/0.85)]"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="hero-fade hero-fade--fast absolute inset-x-0 bottom-8 hidden justify-center md:flex">
        <a
          href="#portal"
          className="hero-in group flex flex-col items-center gap-2 text-[11px] tracking-[0.35em] text-gold/80 uppercase hover:text-gold-bright"
          style={{ "--in-delay": "900ms" } as React.CSSProperties}
        >
          Explorar
          <span aria-hidden className="relative block h-10 w-px overflow-hidden bg-gold/20">
            <span className="scroll-cue__line absolute inset-0 bg-gold-bright" />
          </span>
        </a>
      </div>
    </section>
  );
}
