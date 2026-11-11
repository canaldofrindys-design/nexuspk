import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { clientContext, getSessionToken } from "@/features/auth/session";
import { formatDateTime } from "@/features/home/format";
import { CloseTicketForm, PlayerReplyForm } from "@/features/support/forms";
import { Thread } from "@/features/support/Thread";
import { playerStatusLabels, statusClass, ticketCategories, type PlayerTicket } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Chamado", robots: { index: false } };

async function Body({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ aberto?: string }> }) {
  await connection();
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const token = await getSessionToken();
  if (!token) redirect(`/login?next=/painel/suporte/${id}`);
  const r = await apiSend<PlayerTicket>("GET", `/api/v1/player/tickets/${id}`, { token, ...(await clientContext()) });
  if (!r.ok && r.status === 401) redirect(`/login?next=/painel/suporte/${id}`);
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  const { ticket, messages } = r.data;
  const closed = ticket.status === "fechado";
  return (
    <>
      {(await searchParams).aberto ? (
        <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Chamado #{ticket.id} aberto. Você recebe um e-mail quando a equipe responder (se o e-mail da conta estiver confirmado).</p>
      ) : null}
      <h1 className="mb-1 font-display text-2xl text-gold-bright">{ticket.subject}</h1>
      <dl className="mb-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm sm:grid-cols-[auto_1fr_auto_1fr]">
        <dt className="text-silver/60">Chamado</dt>
        <dd className="tabular text-silver">#{ticket.id}</dd>
        <dt className="text-silver/60">Categoria</dt>
        <dd className="text-silver">{ticketCategories[ticket.category] ?? ticket.category}</dd>
        <dt className="text-silver/60">Situação</dt>
        <dd className={statusClass[ticket.status]}>{playerStatusLabels[ticket.status] ?? ticket.status}</dd>
        <dt className="text-silver/60">Aberto em</dt>
        <dd className="tabular text-silver">{formatDateTime(ticket.createdUtc)}</dd>
      </dl>
      <div className="grid gap-4 lg:grid-cols-[1fr_300px]">
        <GameWindow title="Conversa">
          <Thread messages={messages} viewer="jogador" attachmentHref={(a) => `/painel/suporte/${ticket.id}/anexo/${a}`} />
        </GameWindow>
        <div className="flex flex-col gap-4">
          {closed ? (
            <GameWindow title="Chamado encerrado">
              <p className="text-sm text-silver/70">
                Encerrado em {ticket.closedUtc ? formatDateTime(ticket.closedUtc) : "—"}
                {ticket.closedByPlayer ? " por você" : " pela equipe"}. Se precisar de mais ajuda,{" "}
                <Link href="/painel/suporte/novo" className="text-arcane hover:underline">
                  abra outro chamado
                </Link>
                .
              </p>
            </GameWindow>
          ) : (
            <>
              <GameWindow title="Responder">
                <PlayerReplyForm ticketId={ticket.id} />
              </GameWindow>
              <GameWindow title="Encerrar">
                <CloseTicketForm ticketId={ticket.id} />
              </GameWindow>
            </>
          )}
        </div>
      </div>
    </>
  );
}

export default function PlayerTicketPage({ params, searchParams }: PageProps<"/painel/suporte/[id]">) {
  return (
    <div className="mx-auto w-full max-w-6xl px-4 py-10">
      <p className="mb-2 text-sm">
        <Link href="/painel/suporte" className="text-arcane hover:underline">
          Meus chamados
        </Link>
      </p>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body params={params} searchParams={searchParams as Promise<{ aberto?: string }>} />
      </Suspense>
    </div>
  );
}
