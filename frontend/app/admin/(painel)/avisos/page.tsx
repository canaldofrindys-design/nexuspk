import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import type { AdminList } from "@/features/admin/types";
import { formatDateTime } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { noticeKinds, noticeStatuses, type ServerNotice } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Avisos de status" };

type Q = { pagina?: string; salvo?: string };

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const q = await searchParams;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  const r = await apiSend<AdminList<ServerNotice>>("GET", `/api/v1/admin/notices?pagina=${page}`, { token });
  return (
    <>
      {q.salvo ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Aviso salvo.</p> : null}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-silver/70">
          Manutenções e avisos publicados aparecem em{" "}
          <Link href="/status" className="text-arcane hover:underline">
            /status
          </Link>
          .
        </p>
        <Link href="/admin/avisos/novo" className="border border-gold bg-gold/15 px-3 py-1 text-sm text-gold-bright hover:bg-gold/25">
          Novo aviso
        </Link>
      </div>
      <GameWindow title="Avisos de status">
        {!r.ok ? (
          <p className="text-sm text-data-bad">{r.error.message}</p>
        ) : r.data.items.length === 0 ? (
          <p className="text-sm text-silver/70">Nenhum aviso.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/60">
                  <th scope="col" className="py-1 font-normal">Título</th>
                  <th scope="col" className="py-1 font-normal">Tipo</th>
                  <th scope="col" className="py-1 font-normal">Situação</th>
                  <th scope="col" className="py-1 font-normal">Janela</th>
                  <th scope="col" className="py-1 text-right font-normal">Última edição</th>
                </tr>
              </thead>
              <tbody>
                {r.data.items.map((n) => (
                  <tr key={n.id} className="border-t border-steel/60">
                    <td className="py-1.5">
                      <Link href={`/admin/avisos/${n.id}`} className="text-data-name hover:underline">
                        {n.title}
                      </Link>
                    </td>
                    <td className="py-1.5 text-silver/80">{noticeKinds[n.kind]}</td>
                    <td className="py-1.5 text-silver/80">{noticeStatuses[n.status]}</td>
                    <td className="tabular py-1.5 text-silver/70">
                      {formatDateTime(n.startsUtc)}
                      {n.endsUtc ? ` até ${formatDateTime(n.endsUtc)}` : ""}
                    </td>
                    <td className="tabular py-1.5 text-right text-silver/60">
                      {formatDateTime(n.updatedUtc)} {n.updatedBy ? `por ${n.updatedBy}` : ""}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {r.ok ? <Pager page={r.data.page} pageSize={r.data.pageSize} total={r.data.total} href={(p) => `/admin/avisos?pagina=${p}`} /> : null}
      </GameWindow>
    </>
  );
}

export default function AdminNotices({ searchParams }: PageProps<"/admin/avisos">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
