import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { deleteBannerAction } from "@/features/admin/actions";
import { BannerForm, ConfirmActionForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import type { BannerRecord } from "@/features/site/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Editar banner" };

async function Body({ params }: { params: Promise<{ id: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const r = await apiSend<BannerRecord>("GET", `/api/v1/admin/banners/${id}`, { token });
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  return (
    <>
      <BannerForm banner={r.data} />
      <div className="mt-6 flex flex-wrap items-start justify-between gap-3 border-t border-steel/60 pt-4">
        <p className="max-w-md text-xs text-silver/60">Para tirar da home sem apagar, desmarque &ldquo;Ativo&rdquo;.</p>
        <ConfirmActionForm action={deleteBannerAction} id={r.data.id} label="Excluir banner" confirm={`Excluir o banner "${r.data.title}"?`} />
      </div>
    </>
  );
}

export default function EditBannerPage({ params }: PageProps<"/admin/banners/[id]">) {
  return (
    <GameWindow title="Editar banner">
      <Suspense fallback={null}>
        <Body params={params} />
      </Suspense>
    </GameWindow>
  );
}
