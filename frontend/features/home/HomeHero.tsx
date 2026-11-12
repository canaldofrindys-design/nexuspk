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
    <div className="glass-panel max-w-md p-4 lg:mt-6">
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
 * No celular e no tablet o herói fica centralizado entre o logo e o destaque; no PC, à direita.
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
      <div aria-hidden className="absolute inset-0 -z-10 bg-gradient-to-b from-night/20 via-night/40 to-night/85 lg:bg-gradient-to-r lg:from-night lg:via-night/70 lg:to-transparent" />
      <div aria-hidden className="absolute inset-x-0 bottom-0 -z-10 h-56 bg-gradient-to-t from-night via-night/60 to-transparent" />
      <div aria-hidden className="absolute inset-x-0 top-0 -z-10 h-28 bg-gradient-to-b from-night/80 to-transparent" />
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10 shadow-[inset_0_0_180px_60px_rgb(0_0_0/0.75)]" />
      <div aria-hidden className="absolute inset-0 -z-[5] overflow-hidden">
        <div className="hero-fog hero-fog--front" />
      </div>

      {/*
        Celular: uma coluna centralizada (logo, herói em cena, destaque sobreposto às pernas do
        herói, botões e status), tudo na primeira tela. O bloco de texto vira "contents" para os
        filhos se ordenarem junto com o herói. Do notebook (1024px) em diante, texto à esquerda e herói à direita;
        no tablet vale o layout do celular, mais largo.
      */}
      <div className="relative mx-auto grid w-full max-w-7xl flex-1 content-start items-end gap-x-4 px-4 pt-5 pb-10 text-center lg:grid-cols-[1fr_minmax(0,540px)] lg:content-stretch lg:gap-y-4 lg:pt-12 lg:pb-16 lg:text-left">
        <div className="hero-fade contents lg:block lg:self-center">
          <h1 className="sr-only">NexusPK, servidor privado de Priston Tale</h1>
          <div className="order-1 lg:order-none">
            <span className="logo-shine hero-in">
              <Image
                src="/brand/logo-nexuspk.webp"
                alt=""
                width={400}
                height={161}
                priority
                className="h-auto w-60 drop-shadow-[0_0_32px_rgb(95_178_255/0.55)] min-[380px]:w-64 sm:w-[420px]"
              />
            </span>
          </div>
          <p
            className="hero-in order-2 mt-2 flex items-center justify-center gap-3 font-display text-xs tracking-[0.3em] text-gold uppercase lg:order-none lg:mt-3 lg:justify-start lg:text-sm"
            style={{ "--in-delay": "120ms" } as React.CSSProperties}
          >
            <span aria-hidden className="h-px w-10 bg-gradient-to-r from-transparent to-gold" />
            Priston Tale
            <span aria-hidden className="h-px w-10 bg-gradient-to-l from-transparent to-gold" />
          </p>
          <p
            className="hero-in order-3 mx-auto mt-2 max-w-sm text-[15px] leading-snug text-silver/90 [text-shadow:0_1px_10px_rgb(4_8_20/0.95)] lg:order-none lg:mx-0 lg:mt-3 lg:max-w-lg lg:text-lg lg:text-silver"
            style={{ "--in-delay": "200ms" } as React.CSSProperties}
          >
            {home?.server.tagline || "Servidor privado de Priston Tale."}
          </p>

          <div className="hero-in relative order-5 mx-auto w-full max-w-md text-left lg:order-none lg:mx-0" style={{ "--in-delay": "320ms" } as React.CSSProperties}>
            <Highlight home={home} />
          </div>

          <div
            className="hero-in relative order-6 mx-auto mt-4 grid w-full max-w-md grid-cols-2 gap-3 lg:mx-0 lg:w-auto lg:max-w-none lg:order-none lg:mt-6 lg:flex lg:flex-wrap"
            style={{ "--in-delay": "440ms" } as React.CSSProperties}
          >
            <ButtonLink href="/criar-conta" size="lg" shine className="justify-center px-3! whitespace-nowrap lg:px-5!">
              Criar conta
            </ButtonLink>
            <ButtonLink href="/painel" size="lg" variant="ghost" className="justify-center px-3! whitespace-nowrap lg:px-5!">
              <span className="min-[380px]:hidden">Meu painel</span>
              <span className="hidden min-[380px]:inline">Painel do jogador</span>
            </ButtonLink>
          </div>

          <ul
            className="hero-in relative order-7 mt-4 flex flex-wrap justify-center gap-x-5 gap-y-1 text-sm text-silver/85 lg:order-none lg:mt-6 lg:justify-start lg:gap-x-6"
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
            <li className="hidden lg:list-item">Horários de Brasília</li>
          </ul>
        </div>

        {/* Herói: no celular fica em cena atrás do destaque (pernas somem na névoa); no PC, à direita. */}
        <div
          aria-hidden
          className="hero-stage px-layer pointer-events-none relative -z-[1] order-4 -mx-4 mt-0 -mb-16 sm:mt-6 lg:pointer-events-auto lg:z-auto lg:order-none lg:m-0 lg:self-end"
        >
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
                  sizes="(min-width: 1024px) 540px, (min-width: 768px) 70vw, 100vw"
                  className="hero-figure mx-auto h-auto w-full drop-shadow-[0_24px_40px_rgb(0_0_0/0.85)]"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="hero-fade hero-fade--fast absolute inset-x-0 bottom-8 hidden justify-center lg:flex">
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
