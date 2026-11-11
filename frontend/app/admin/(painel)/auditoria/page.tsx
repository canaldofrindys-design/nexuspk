import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import type { AdminList, AuditEntry } from "@/features/admin/types";
import { formatDateTime } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Auditoria" };

type Q = { tipo?: string; autor?: string; acao?: string; pagina?: string };

const actorTypes: Record<string, string> = { "": "Todos", staff: "Equipe", player: "Jogador", system: "Sistema" };

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const { token } = await requireStaff("ViewAudit");
  const q = await searchParams;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  const filters: Record<string, string> = {};
  if (q.tipo && q.tipo in actorTypes) filters.tipo = q.tipo;
  if (q.autor) filters.autor = q.autor.slice(0, 64);
  if (q.acao) filters.acao = q.acao.slice(0, 100);
  const r = await apiSend<AdminList<AuditEntry>>("GET", `/api/v1/admin/audit?${new URLSearchParams({ ...filters, pagina: String(page) })}`, { token });

  return (
    <GameWindow title="Auditoria">
      <form method="get" className="mb-4 flex flex-wrap items-end gap-3 text-sm">
        <label className="flex flex-col gap-1">
          <span className="text-silver/70">Quem</span>
          <select name="tipo" defaultValue={filters.tipo ?? ""} className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1.5">
            {Object.entries(actorTypes).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-silver/70">Usuário ou login</span>
          <input name="autor" defaultValue={filters.autor} maxLength={64} className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1.5" />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-silver/70">Ação começa com</span>
          <input name="acao" defaultValue={filters.acao} maxLength={100} placeholder="ex.: staff." className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1.5" />
        </label>
        <button type="submit" className="border border-bronze px-3 py-1.5 text-gold hover:border-gold">
          Filtrar
        </button>
      </form>
      {!r.ok ? (
        <p className="text-sm text-data-bad">{r.error.message}</p>
      ) : r.data.items.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum registro.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="text-left text-xs text-silver/60">
                <th scope="col" className="py-1 font-normal">Quando</th>
                <th scope="col" className="py-1 font-normal">Quem</th>
                <th scope="col" className="py-1 font-normal">Ação</th>
                <th scope="col" className="py-1 font-normal">Alvo</th>
                <th scope="col" className="py-1 font-normal">IP</th>
                <th scope="col" className="py-1 font-normal">Detalhes</th>
              </tr>
            </thead>
            <tbody>
              {r.data.items.map((a) => (
                <tr key={a.id} className="border-t border-steel/60 align-top">
                  <td className="tabular whitespace-nowrap py-1.5 pr-3 text-silver/70">{formatDateTime(a.occurredUtc)}</td>
                  <td className="py-1.5 pr-3">
                    <span className="text-xs text-silver/50">{actorTypes[a.actorType] ?? a.actorType}</span> {a.actorId ?? "—"}
                  </td>
                  <td className="py-1.5 pr-3 font-mono text-xs text-data-info">{a.action}</td>
                  <td className="py-1.5 pr-3 text-silver/70">{a.targetType ? `${a.targetType} ${a.targetId ?? ""}` : "—"}</td>
                  <td className="tabular py-1.5 pr-3 text-silver/60">{a.ipAddress ?? "—"}</td>
                  <td className="max-w-xs break-all py-1.5 font-mono text-xs text-silver/60">{a.details ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {r.ok ? (
        <Pager page={r.data.page} pageSize={r.data.pageSize} total={r.data.total} href={(p) => `/admin/auditoria?${new URLSearchParams({ ...filters, pagina: String(p) })}`} />
      ) : null}
    </GameWindow>
  );
}

export default function AuditPage({ searchParams }: PageProps<"/admin/auditoria">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
