import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { NoticeForm } from "@/features/support/forms";
import type { ServerNotice } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Editar aviso" };

async function Body({ params }: { params: Promise<{ id: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const r = await apiSend<ServerNotice>("GET", `/api/v1/admin/notices/${id}`, { token });
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  return <NoticeForm notice={r.data} />;
}

export default function EditNoticePage({ params }: PageProps<"/admin/avisos/[id]">) {
  return (
    <GameWindow title="Editar aviso">
      <Suspense fallback={null}>
        <Body params={params} />
      </Suspense>
    </GameWindow>
  );
}
