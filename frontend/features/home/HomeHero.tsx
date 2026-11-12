import Image from "next/image";
import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Countdown } from "@/features/content/Countdown";
import { eventLabel, newsLabel } from "@/features/content/labels";
import { formatDateTime, formatNumber } from "./format";
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
 * Abertura da home: cenário do castelo em tela larga, logo, o fato do momento,
 * ações de conta e o estado do servidor. O herói aparece só a partir do tablet.
 */
export function HomeHero({ home }: { home: HomeSnapshot | null }) {
  const state = availability[home?.status.availability ?? "unknown"];
  const players = home?.status.playersOnline;

  return (
    <section aria-label="Destaque" className="relative isolate overflow-hidden border-b border-gold/40">
      <Image
        src="/art/castelo.webp"
        alt=""
        fill
        priority
        sizes="100vw"
        className="-z-20 object-cover object-[60%_40%]"
      />
      <div aria-hidden className="absolute inset-0 -z-10 bg-gradient-to-r from-night via-night/75 to-night/10" />
      <div aria-hidden className="absolute inset-x-0 bottom-0 -z-10 h-48 bg-gradient-to-t from-night to-transparent" />
      <div aria-hidden className="absolute inset-x-0 top-0 -z-10 h-24 bg-gradient-to-b from-night/70 to-transparent" />

      <div className="mx-auto grid max-w-7xl items-end gap-4 px-4 pt-8 pb-10 md:min-h-[600px] md:grid-cols-[1fr_minmax(0,520px)] md:pt-12">
        <div className="self-center">
          <h1 className="sr-only">NexusPK, servidor privado de Priston Tale</h1>
          <Image
            src="/brand/logo-nexuspk.webp"
            alt=""
            width={400}
            height={161}
            priority
            className="h-auto w-64 drop-shadow-[0_0_28px_rgb(95_178_255/0.45)] sm:w-[400px]"
          />
          <p className="mt-3 max-w-lg text-lg text-silver">{home?.server.tagline || "Servidor privado de Priston Tale."}</p>

          <Highlight home={home} />

          <div className="mt-6 flex flex-wrap gap-3">
            <ButtonLink href="/criar-conta" size="lg">
              Criar conta
            </ButtonLink>
            <ButtonLink href="/painel" size="lg" variant="ghost">
              Painel do jogador
            </ButtonLink>
          </div>

          <ul className="mt-6 flex flex-wrap gap-x-6 gap-y-1 text-sm text-silver/85">
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

        <Image
          src="/art/heroi-azul.webp"
          alt=""
          width={1318}
          height={1100}
          priority
          sizes="(min-width: 768px) 520px, 0px"
          className="hidden h-auto w-full self-end drop-shadow-[0_20px_40px_rgb(0_0_0/0.8)] md:block"
        />
      </div>
    </section>
  );
}
