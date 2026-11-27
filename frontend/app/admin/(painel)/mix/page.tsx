import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { MixForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import { formatDateTime } from "@/features/home/format";
import type { MixSettings, SettingView } from "@/features/site/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Mix List" };

async function Body() {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const r = await apiSend<SettingView<MixSettings>>("GET", "/api/v1/admin/site/mix", { token });
  return (
    <GameWindow
      title="Mix List"
      actions={
        <Link href="/mix" className="text-xs text-arcane hover:underline">
          Ver página
        </Link>
      }
    >
      {r.ok ? (
        <>
          <p className="mb-3 text-xs text-silver/60">
            {r.data.updatedUtc
              ? `Última alteração em ${formatDateTime(r.data.updatedUtc)}${r.data.updatedBy ? ` por ${r.data.updatedBy}` : ""}.`
              : "Ainda vazia: a página pública avisa que a lista está sendo publicada."}
          </p>
          <MixForm settings={r.data.value} />
        </>
      ) : (
        <p className="text-sm text-data-bad">{r.error.message}</p>
      )}
    </GameWindow>
  );
}

export default function AdminMixPage() {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body />
    </Suspense>
  );
}
