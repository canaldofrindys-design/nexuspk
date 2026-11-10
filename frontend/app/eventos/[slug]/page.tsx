import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { getCurrentPlayer, getSessionToken } from "@/features/auth/session";
import { Countdown } from "@/features/content/Countdown";
import { Prose } from "@/features/content/Prose";
import { ReminderForm } from "@/features/content/ReminderForm";
import { formatLong } from "@/features/content/dates";
import { eventLabel, eventStates, safeImage } from "@/features/content/labels";
import type { EventDetail } from "@/features/content/types";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

const slugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

async function load(slug: string, token?: string) {
  if (!slugPattern.test(slug) || slug.length > 120) return null;
  const result = await apiSend<EventDetail>("GET", `/api/v1/events/${slug}`, { token });
  if (!result.ok && result.status === 404) return null;
  return result;
}

export async function generateMetadata({ params }: PageProps<"/eventos/[slug]">): Promise<Metadata> {
  const result = await load((await params).slug);
  return result?.ok ? { title: result.data.event.name, description: result.data.event.summary } : { title: "Evento" };
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-silver/70">{label}</dt>
      <dd className="text-right text-silver">{children}</dd>
    </>
  );
}

async function Reminder({ detail }: { detail: EventDetail }) {
  const e = detail.event;
  if (e.state !== "scheduled") return null;
  const player = await getCurrentPlayer();
  if (!player) {
    return (
      <p className="text-sm text-silver/80">
        <Link href={`/login?next=/eventos/${e.slug}`} className="text-arcane hover:underline">
          Entre na sua conta
        </Link>{" "}
        para receber um e-mail cerca de uma hora antes do início.
      </p>
    );
  }
  if (!player.emailConfirmed) {
    return (
      <p className="text-sm text-silver/80">
        Para receber lembretes, confirme um e-mail no{" "}
        <Link href="/painel" className="text-arcane hover:underline">
          painel da conta
        </Link>
        .
      </p>
    );
  }
  return <ReminderForm slug={e.slug} active={detail.reminderSet === true} />;
}

async function Detail({ params }: { params: Promise<{ slug: string }> }) {
  await connection();
  const token = await getSessionToken();
  const result = await load((await params).slug, token);
  if (!result) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Evento indisponível no momento. Tente de novo em instantes.</p>;
  const d = result.data;
  const e = d.event;
  const state = eventStates[e.state];
  const image = safeImage(e.imageUrl);
  const now = new Date().toISOString();

  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_340px]">
      <article className="game-window self-start">
        <header className="game-window__title px-4 py-3">
          <p className="mb-1 flex flex-wrap items-center gap-2 text-xs">
            <span className="border border-amethyst/60 px-1.5 text-amethyst">{eventLabel(e.category)}</span>
            <span className={state.tone}>{state.label}</span>
          </p>
          <h1 className="text-xl text-gold-bright">{e.name}</h1>
        </header>
        <div className="flex flex-col gap-5 p-4">
          {image ? (
            // eslint-disable-next-line @next/next/no-img-element -- imagem cadastrada pela equipe, tamanho livre
            <img src={image} alt="" className="h-auto w-full border border-steel/60" loading="lazy" decoding="async" width={1200} height={500} />
          ) : null}
          <p className="text-sm text-silver/80">{e.summary}</p>
          <section aria-labelledby="sec-desc">
            <h2 id="sec-desc" className="mb-2 text-sm text-gold">Descrição</h2>
            <Prose text={d.description} />
          </section>
          {d.rules ? (
            <section aria-labelledby="sec-rules">
              <h2 id="sec-rules" className="mb-2 text-sm text-gold">Regulamento</h2>
              <Prose text={d.rules} />
            </section>
          ) : null}
          {d.prizes ? (
            <section aria-labelledby="sec-prizes">
              <h2 id="sec-prizes" className="mb-2 text-sm text-gold">Premiação</h2>
              <Prose text={d.prizes} />
            </section>
          ) : null}
          <p className="text-xs text-silver/50">Atualizado em {formatDateTime(d.updatedUtc)}.</p>
        </div>
      </article>

      <div className="flex flex-col gap-4">
        <GameWindow title="Quando">
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <Row label="Início">{formatLong(e.startsUtc)}</Row>
            {e.endsUtc ? <Row label="Fim">{formatLong(e.endsUtc)}</Row> : null}
            {e.location ? <Row label="Local">{e.location}</Row> : null}
            <Row label="Situação">
              <span className={state.tone}>{state.label}</span>
            </Row>
          </dl>
          <div className="mt-3">
            {e.state === "scheduled" ? <Countdown target={e.startsUtc} label="Começa em" serverNow={now} /> : null}
            {e.state === "live" && e.endsUtc ? <Countdown target={e.endsUtc} label="Termina em" serverNow={now} /> : null}
          </div>
          <p className="mt-3 text-xs text-silver/60">Horário de Brasília.</p>
          {e.state !== "cancelled" && e.state !== "ended" ? (
            <div className="mt-3">
              <a
                href={`/eventos/${e.slug}/ics`}
                download
                className="inline-flex items-center gap-2 rounded-sm border border-bronze bg-night/60 px-3.5 py-1.5 text-sm font-semibold text-gold hover:border-gold"
              >
                Adicionar à minha agenda (.ics)
              </a>
            </div>
          ) : null}
        </GameWindow>
        {e.state === "scheduled" ? (
          <GameWindow title="Lembrete">
            <Suspense fallback={null}>
              <Reminder detail={d} />
            </Suspense>
          </GameWindow>
        ) : null}
        <GameWindow title="Como funciona">
          <p className="text-sm text-silver/80">
            O evento é conduzido pela equipe dentro do jogo no horário divulgado. O site só publica a agenda: cadastrar ou alterar um evento
            aqui não liga nada no servidor.
          </p>
        </GameWindow>
      </div>
    </div>
  );
}

export default function EventPage({ params }: PageProps<"/eventos/[slug]">) {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <Link href="/eventos" className="mb-4 inline-block text-sm text-arcane hover:underline">
        Todos os eventos
      </Link>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Detail params={params} />
      </Suspense>
    </div>
  );
}
