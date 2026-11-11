import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { formatDateTime } from "@/features/home/format";
import { StaffReplyForm, TicketControlsForm } from "@/features/support/forms";
import { Thread } from "@/features/support/Thread";
import { statusClass, ticketCategories, ticketStatuses, type StaffTicket, type SupportAgent } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Chamado" };

async function Body({ params }: { params: Promise<{ id: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageSupport");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const [r, agents] = await Promise.all([
    apiSend<StaffTicket>("GET", `/api/v1/admin/tickets/${id}`, { token }),
    apiSend<SupportAgent[]>("GET", "/api/v1/admin/tickets/agents", { token }),
  ]);
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  const t = r.data.ticket;
  return (
    <>
      <h1 className="mb-1 font-display text-xl text-gold-bright">
        #{t.id} {t.subject}
      </h1>
      <p className="mb-4 text-sm text-silver/70">
        Jogador <span className="text-data-name">{t.gameLogin}</span>, {ticketCategories[t.category] ?? t.category}, aberto em {formatDateTime(t.createdUtc)}.{" "}
        <Link href={`/admin/chamados?situacao=todos&login=${encodeURIComponent(t.gameLogin)}`} className="text-arcane hover:underline">
          Outros chamados desta conta
        </Link>
      </p>
      <div className="grid gap-4 lg:grid-cols-[1fr_300px]">
        <div className="flex min-w-0 flex-col gap-4">
          <GameWindow title="Conversa">
            <Thread messages={r.data.messages} viewer="equipe" attachmentHref={(a) => `/admin/chamados/${t.id}/anexo/${a}`} />
          </GameWindow>
          <GameWindow title="Responder">
            <StaffReplyForm ticketId={t.id} />
          </GameWindow>
        </div>
        <div className="flex flex-col gap-4">
          <GameWindow title="Atendimento">
            <p className={`mb-3 text-sm font-semibold ${statusClass[t.status] ?? ""}`}>{ticketStatuses[t.status] ?? t.status}</p>
            <TicketControlsForm ticket={t} agents={agents.ok ? agents.data : []} />
            {t.closedUtc ? (
              <p className="mt-3 text-xs text-silver/60">
                Fechado em {formatDateTime(t.closedUtc)} por {t.closedBy ?? "—"}.
              </p>
            ) : null}
          </GameWindow>
          <GameWindow title="Lembretes">
            <ul className="flex list-disc flex-col gap-1 pl-4 text-xs text-silver/70">
              <li>Nunca peça a senha do jogador.</li>
              <li>Mudanças em conta, itens ou personagens são feitas no servidor do jogo pela equipe autorizada; o portal só registra o atendimento.</li>
              <li>Toda resposta e mudança de situação fica na auditoria.</li>
            </ul>
          </GameWindow>
        </div>
      </div>
    </>
  );
}

export default function AdminTicketPage({ params }: PageProps<"/admin/chamados/[id]">) {
  return (
    <>
      <p className="mb-2 text-sm">
        <Link href="/admin/chamados" className="text-arcane hover:underline">
          Fila de chamados
        </Link>
      </p>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body params={params} />
      </Suspense>
    </>
  );
}
