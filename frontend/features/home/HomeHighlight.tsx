import Image from "next/image";
import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { Countdown } from "@/features/content/Countdown";
import { eventLabel, newsLabel } from "@/features/content/labels";
import { formatDateTime, formatNumber } from "./format";
import type { HomeSnapshot } from "./types";

const availability = {
  online: { text: "Servidor online", dot: "bg-data-ok" },
  offline: { text: "Servidor offline", dot: "bg-data-bad" },
  partial: { text: "Servidor parcialmente fora do ar", dot: "bg-data-name" },
  unknown: { text: "Status em verificação", dot: "bg-silver/50" },
} as const;

const eventTag = "border-amethyst/60 text-amethyst";

/**
 * Destaque da home: o fato do momento (evento em andamento, próximo evento ou última notícia)
 * e o estado do servidor. Sem dado publicado, mostra só o nome do servidor.
 */
export function HomeHighlight({ home }: { home: HomeSnapshot | null }) {
  const now = new Date().toISOString();
  const live = home?.activeEvents[0];
  const next = home?.upcomingEvents[0];
  const news = home?.news[0];
  const state = availability[home?.status.availability ?? "unknown"];
  const players = home?.status.playersOnline;

  return (
    <section aria-label="Destaque" className="relative overflow-hidden border-b border-bronze/60 bg-[radial-gradient(ellipse_at_75%_40%,#3a2a16_0,transparent_60%)]">
      <div className="mx-auto grid max-w-7xl items-center gap-6 px-4 py-6 md:grid-cols-[1fr_auto] md:py-8">
        <div>
          {live ? (
            <>
              <span className={`inline-block border px-2 text-xs ${eventTag}`}>{eventLabel(live.category)}</span>{" "}
              <span className="ml-1 text-xs text-data-ok">Em andamento</span>
              <h1 className="mt-2 font-display text-3xl text-data-name sm:text-4xl">{live.name}</h1>
              {live.endsAtUtc ? (
                <div className="mt-3">
                  <Countdown target={live.endsAtUtc} label="Termina em" serverNow={now} />
                </div>
              ) : null}
              <div className="mt-4">
                <ButtonLink href={`/eventos/${live.slug}`}>Ver regras do evento</ButtonLink>
              </div>
            </>
          ) : next ? (
            <>
              <span className={`inline-block border px-2 text-xs ${eventTag}`}>{eventLabel(next.category)}</span>
              <h1 className="mt-2 font-display text-3xl text-data-name sm:text-4xl">{next.name}</h1>
              <p className="tabular mt-1 text-silver">Começa {formatDateTime(next.startsAtUtc)} (horário de Brasília)</p>
              <div className="mt-3">
                <Countdown target={next.startsAtUtc} label="Começa em" serverNow={now} />
              </div>
              <div className="mt-4">
                <ButtonLink href={`/eventos/${next.slug}`}>Ver regras do evento</ButtonLink>
              </div>
            </>
          ) : news ? (
            <>
              <span className={`inline-block border px-2 text-xs ${newsLabel(news.category).tone}`}>{newsLabel(news.category).label}</span>
              <h1 className="mt-2 font-display text-3xl text-data-name sm:text-4xl">{news.title}</h1>
              {news.summary ? <p className="mt-2 max-w-2xl text-silver">{news.summary}</p> : null}
              <p className="mt-4">
                <Link href={`/noticias/${news.slug}`} className="text-arcane hover:underline">
                  Ler a notícia completa
                </Link>
              </p>
            </>
          ) : (
            <>
              <h1 className="font-display text-4xl text-gold-bright sm:text-5xl">{home?.server.name || "NexusPK"}</h1>
              <p className="mt-2 text-silver">{home?.server.tagline || "Servidor privado de Priston Tale."}</p>
            </>
          )}

          <ul className="mt-5 flex flex-wrap gap-x-5 gap-y-1 text-sm text-silver/80">
            <li className="flex items-center gap-2">
              <span aria-hidden className={`inline-block size-2.5 rounded-full ${state.dot}`} />
              {state.text}
            </li>
            {players != null ? <li className="tabular">{formatNumber(players)} jogadores online</li> : null}
            <li>Horários de Brasília</li>
          </ul>
        </div>
        <Image
          src="/classes/cavaleiro.webp"
          alt=""
          width={556}
          height={900}
          priority
          className="hidden h-[300px] w-auto md:block"
        />
      </div>
    </section>
  );
}
