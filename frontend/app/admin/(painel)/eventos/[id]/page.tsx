import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { EventForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import type { EventRecord } from "@/features/admin/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Editar evento" };

async function Body({ params }: { params: Promise<{ id: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const r = await apiSend<EventRecord>("GET", `/api/v1/admin/events/${id}`, { token });
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  return (
    <>
      {r.data.status !== "draft" ? (
        <p className="mb-3 text-sm">
          <Link href={`/eventos/${r.data.slug}`} className="text-arcane hover:underline">
            Ver no site
          </Link>
        </p>
      ) : null}
      <EventForm event={r.data} />
    </>
  );
}

export default function EditEventPage({ params }: PageProps<"/admin/eventos/[id]">) {
  return (
    <GameWindow title="Editar evento">
      <Suspense fallback={null}>
        <Body params={params} />
      </Suspense>
    </GameWindow>
  );
}
