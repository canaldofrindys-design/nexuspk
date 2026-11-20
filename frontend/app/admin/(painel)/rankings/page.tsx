import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { ConfirmDialog, RankingResetForm, RankingRestoreForm } from "@/features/admin/gameForms";
import { requireStaff } from "@/features/admin/session";
import type { RankingsPage } from "@/features/admin/types";
import { Notice, PageTitle, Pill } from "@/features/admin/ui";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Reset de rankings" };

async function Body() {
  await connection();
  const { token } = await requireStaff("ResetRankings");
  const r = await apiSend<RankingsPage>("GET", "/api/v1/admin/rankings", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  const d = r.data;
  const label = (k: string) => d.kinds.find((x) => x.key === k)?.label ?? k;
  return (
    <div className="flex flex-col gap-4">
      {!d.enabled ? (
        <Notice title="Função desligada">O login de administração do jogo não está configurado na VPS. Veja docs/ADMIN.md, seção &quot;Ligar as funções do jogo&quot;.</Notice>
      ) : null}
      <div className="grid gap-3 md:grid-cols-2">
        {d.kinds.map((k) => (
          <div key={k.key} className="flex flex-col justify-between gap-3 rounded-md border border-steel/70 bg-night/50 p-4">
            <div>
              <p className="text-silver">{k.label}</p>
              <p className="mt-1 text-xs text-silver/60">{k.description}</p>
            </div>
            {d.enabled ? (
              <div>
                <ConfirmDialog label="Zerar" title={`Zerar ${k.label}`} tone="danger" small>
                  <RankingResetForm kind={k.key} label={k.label} word={d.confirmWord} />
                </ConfirmDialog>
              </div>
            ) : null}
          </div>
        ))}
      </div>
      <GameWindow title="Cópias de segurança">
        {d.backups.length === 0 ? (
          <p className="text-sm text-silver/60">Nenhum reset feito pelo painel ainda.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/55">
                  <th scope="col" className="py-1 font-normal">Nº</th>
                  <th scope="col" className="py-1 font-normal">Ranking</th>
                  <th scope="col" className="py-1 font-normal">Quando / quem</th>
                  <th scope="col" className="py-1 text-right font-normal">Linhas</th>
                  <th scope="col" className="py-1 pl-3 font-normal">Situação</th>
                </tr>
              </thead>
              <tbody>
                {d.backups.map((b) => (
                  <tr key={b.id} className="border-t border-steel/60">
                    <td className="tabular py-1.5">{b.id}</td>
                    <td className="py-1.5">{label(b.kind)}</td>
                    <td className="py-1.5 text-silver/70">
                      {formatDateTime(b.createdUtc)} · {b.createdBy}
                    </td>
                    <td className="tabular py-1.5 text-right">{b.rows}</td>
                    <td className="py-1.5 pl-3">
                      {b.restoredUtc ? (
                        <Pill tone="mute">Desfeito por {b.restoredBy}</Pill>
                      ) : d.enabled ? (
                        <ConfirmDialog label="Desfazer" title={`Desfazer reset nº ${b.id}`} tone="calm" small>
                          <RankingRestoreForm id={b.id} />
                        </ConfirmDialog>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </GameWindow>
    </div>
  );
}

export default function RankingsAdminPage() {
  return (
    <>
      <PageTitle title="Reset de rankings">Zera rankings do jogo. Antes de zerar, o painel guarda uma cópia dos valores para poder desfazer.</PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </>
  );
}
