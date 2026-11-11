import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { CreateStaffForm, EditStaffForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import { roleLabels, type StaffItem } from "@/features/admin/types";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Equipe" };

async function Body() {
  await connection();
  const { me, token } = await requireStaff("ManageStaff");
  const r = await apiSend<StaffItem[]>("GET", "/api/v1/admin/staff", { token });
  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_340px]">
      <div className="flex flex-col gap-4">
        {!r.ok ? <p className="text-sm text-data-bad">{r.error.message}</p> : null}
        {r.ok
          ? r.data.map((m) => (
              <GameWindow key={m.id} title={`${m.displayName} (${m.username})`}>
                <p className="mb-3 text-xs text-silver/60">
                  {roleLabels[m.role] ?? m.role} · {m.active ? "ativo" : "desativado"}
                  {m.mustChangePassword ? " · senha temporária" : ""} · último acesso {m.lastLoginUtc ? formatDateTime(m.lastLoginUtc) : "nunca"}
                </p>
                <EditStaffForm member={m} self={m.id === me.id} />
              </GameWindow>
            ))
          : null}
      </div>
      <div className="flex flex-col gap-4">
        <GameWindow title="Adicionar membro">
          <CreateStaffForm />
        </GameWindow>
        <GameWindow title="Funções">
          <dl className="flex flex-col gap-2 text-sm">
            <dt className="text-gold">Superadministrador</dt>
            <dd className="text-silver/80">Tudo, inclusive a equipe.</dd>
            <dt className="text-gold">Administrador</dt>
            <dd className="text-silver/80">Resumo, notícias, eventos e auditoria.</dd>
            <dt className="text-gold">Moderador</dt>
            <dd className="text-silver/80">Resumo, notícias e eventos.</dd>
            <dt className="text-gold">Suporte</dt>
            <dd className="text-silver/80">Resumo. Os chamados entram com a central de suporte.</dd>
          </dl>
        </GameWindow>
      </div>
    </div>
  );
}

export default function StaffPage() {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body />
    </Suspense>
  );
}
