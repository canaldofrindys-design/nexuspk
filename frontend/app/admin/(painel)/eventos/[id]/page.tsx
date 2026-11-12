import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { deleteEventAction, endEventAction } from "@/features/admin/actions";
import { ConfirmActionForm, EventForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import type { EventRecord } from "@/features/admin/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Editar evento" };

async function Body({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ encerrado?: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const r = await apiSend<EventRecord>("GET", `/api/v1/admin/events/${id}`, { token });
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  const ended = (await searchParams).encerrado;
  const now = new Date().getTime();
  const starts = Date.parse(r.data.startsUtc);
  const ends = r.data.endsUtc ? Date.parse(r.data.endsUtc) : starts + 60 * 60 * 1000;
  const running = r.data.status === "published" && starts < now && now < ends;
  return (
    <>
      {ended ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Evento encerrado agora. O site já mostra como encerrado.</p> : null}
      {running ? (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3 border border-data-ok/50 bg-data-ok/5 px-3 py-2">
          <p className="text-sm text-data-ok">Este evento está acontecendo agora.</p>
          <ConfirmActionForm action={endEventAction} id={r.data.id} tone="neutral" label="Encerrar agora" confirm={`Encerrar agora o evento "${r.data.name}"? O horário de fim passa a ser este minuto.`} />
        </div>
      ) : null}
      {r.data.status !== "draft" ? (
        <p className="mb-3 text-sm">
          <Link href={`/eventos/${r.data.slug}`} className="text-arcane hover:underline">
            Ver no site
          </Link>
        </p>
      ) : null}
      <EventForm key={r.data.updatedUtc} event={r.data} />
      <div className="mt-6 flex flex-wrap items-start justify-between gap-3 border-t border-steel/60 pt-4">
        <p className="max-w-md text-xs text-silver/60">Para avisar que não vai acontecer, mude a situação para Cancelado. Excluir apaga o evento e os lembretes de vez.</p>
        <ConfirmActionForm action={deleteEventAction} id={r.data.id} label="Excluir evento" confirm={`Excluir de vez o evento "${r.data.name}"? Não dá para desfazer.`} />
      </div>
    </>
  );
}

export default function EditEventPage({ params, searchParams }: PageProps<"/admin/eventos/[id]">) {
  return (
    <GameWindow title="Editar evento">
      <Suspense fallback={null}>
        <Body params={params} searchParams={searchParams as Promise<{ encerrado?: string }>} />
      </Suspense>
    </GameWindow>
  );
}
