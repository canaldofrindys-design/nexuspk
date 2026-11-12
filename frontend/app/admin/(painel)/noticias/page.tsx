import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { newsStatusLabels, type AdminList, type NewsRecord } from "@/features/admin/types";
import { newsLabel } from "@/features/content/labels";
import { formatDateTime } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Notícias" };

type Q = { situacao?: string; pagina?: string; salvo?: string; excluido?: string };

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const q = await searchParams;
  const status = q.situacao && q.situacao in newsStatusLabels ? q.situacao : undefined;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  const qs = new URLSearchParams({ pagina: String(page) });
  if (status) qs.set("situacao", status);
  const r = await apiSend<AdminList<NewsRecord>>("GET", `/api/v1/admin/news?${qs}`, { token });
  const href = (s?: string, p = 1) => `/admin/noticias?${new URLSearchParams({ ...(s ? { situacao: s } : {}), ...(p > 1 ? { pagina: String(p) } : {}) })}`;

  return (
    <>
      {q.salvo ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Notícia salva.</p> : null}
      {q.excluido ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Notícia excluída.</p> : null}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <Tabs
          label="Situação"
          tabs={[{ href: href(), label: "Todas", active: !status }, ...Object.entries(newsStatusLabels).map(([k, label]) => ({ href: href(k), label, active: status === k }))]}
        />
        <Link href="/admin/noticias/nova" className="border border-gold bg-gold/15 px-3 py-1 text-sm text-gold-bright hover:bg-gold/25">
          Nova notícia
        </Link>
      </div>
      <GameWindow title="Notícias">
        {!r.ok ? (
          <p className="text-sm text-data-bad">{r.error.message}</p>
        ) : r.data.items.length === 0 ? (
          <p className="text-sm text-silver/70">Nenhuma notícia.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/60">
                  <th scope="col" className="py-1 font-normal">Título</th>
                  <th scope="col" className="py-1 font-normal">Categoria</th>
                  <th scope="col" className="py-1 font-normal">Situação</th>
                  <th scope="col" className="py-1 font-normal">Publicação</th>
                  <th scope="col" className="py-1 text-right font-normal">Última edição</th>
                </tr>
              </thead>
              <tbody>
                {r.data.items.map((n) => (
                  <tr key={n.id} className="border-t border-steel/60">
                    <td className="py-1.5">
                      <Link href={`/admin/noticias/${n.id}`} className="text-data-name hover:underline">
                        {n.title}
                      </Link>
                      {n.pinned ? <span className="ml-2 text-xs text-gold">fixada</span> : null}
                    </td>
                    <td className="py-1.5 text-silver/80">{newsLabel(n.category).label}</td>
                    <td className="py-1.5 text-silver/80">{newsStatusLabels[n.status]}</td>
                    <td className="tabular py-1.5 text-silver/70">{n.publishedUtc ? formatDateTime(n.publishedUtc) : "—"}</td>
                    <td className="tabular py-1.5 text-right text-silver/60">
                      {formatDateTime(n.updatedUtc)} {n.updatedBy ? `por ${n.updatedBy}` : ""}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {r.ok ? <Pager page={r.data.page} pageSize={r.data.pageSize} total={r.data.total} href={(p) => href(status, p)} /> : null}
      </GameWindow>
    </>
  );
}

export default function AdminNews({ searchParams }: PageProps<"/admin/noticias">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
