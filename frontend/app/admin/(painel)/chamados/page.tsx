import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { formatDateTime } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import { statusClass, ticketCategories, ticketPriorities, ticketStatuses, type TicketList } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Chamados" };

type Q = { situacao?: string; categoria?: string; meus?: string; login?: string; esperando?: string; pagina?: string };

const views: Record<string, string> = { esperando: "Esperando a equipe", ativos: "Em andamento", ...ticketStatuses, todos: "Todos" };

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const { me, token } = await requireStaff("ManageSupport");
  const q = await searchParams;
  const view = q.situacao && q.situacao in views ? q.situacao : "esperando";
  const category = q.categoria && q.categoria in ticketCategories ? q.categoria : undefined;
  const mine = q.meus === "1";
  const login = q.login?.trim().slice(0, 32) || undefined;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);

  const api = new URLSearchParams({ pagina: String(page) });
  if (view === "esperando") api.set("aguardandoEquipe", "true");
  else if (view !== "todos") api.set("situacao", view);
  if (category) api.set("categoria", category);
  if (mine) api.set("atendente", String(me.id));
  if (login) api.set("login", login);
  const r = await apiSend<TicketList>("GET", `/api/v1/admin/tickets?${api}`, { token });

  const href = (o: Partial<Q> & { p?: number }) => {
    const s = new URLSearchParams();
    const v = o.situacao ?? view;
    if (v !== "esperando") s.set("situacao", v);
    const c = "categoria" in o ? o.categoria : category;
    if (c) s.set("categoria", c);
    if ("meus" in o ? o.meus === "1" : mine) s.set("meus", "1");
    if (login) s.set("login", login);
    if (o.p && o.p > 1) s.set("pagina", String(o.p));
    return `/admin/chamados?${s}`;
  };

  return (
    <>
      <div className="mb-3 flex flex-col gap-3">
        <Tabs label="Fila" tabs={Object.entries(views).map(([k, label]) => ({ href: href({ situacao: k }), label, active: view === k }))} />
        <div className="flex flex-wrap items-end gap-3 text-sm">
          <Tabs
            label="Categoria"
            tabs={[{ href: href({ categoria: undefined }), label: "Todas", active: !category }, ...Object.entries(ticketCategories).map(([k, label]) => ({ href: href({ categoria: k }), label, active: category === k }))]}
          />
          <Link href={href({ meus: mine ? "0" : "1" })} className={`border px-3 py-1 ${mine ? "border-gold text-gold-bright" : "border-bronze/60 text-silver/80 hover:border-gold"}`}>
            {mine ? "Só os meus (ativo)" : "Só os meus"}
          </Link>
          <form action="/admin/chamados" className="flex items-end gap-2">
            <input type="hidden" name="situacao" value={view} />
            <label className="flex flex-col gap-1 text-xs text-silver/70">
              Login do jogador
              <input name="login" defaultValue={login} maxLength={32} className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1 text-sm text-silver" />
            </label>
            <button type="submit" className="border border-bronze/60 px-3 py-1 text-silver/80 hover:border-gold">
              Filtrar
            </button>
          </form>
        </div>
      </div>
      <GameWindow title="Chamados">
        {!r.ok ? (
          <p className="text-sm text-data-bad">{r.error.message}</p>
        ) : r.data.items.length === 0 ? (
          <p className="text-sm text-silver/70">Nenhum chamado nesta fila.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[820px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/60">
                  <th scope="col" className="py-1 font-normal">Nº</th>
                  <th scope="col" className="py-1 font-normal">Assunto</th>
                  <th scope="col" className="py-1 font-normal">Jogador</th>
                  <th scope="col" className="py-1 font-normal">Categoria</th>
                  <th scope="col" className="py-1 font-normal">Situação</th>
                  <th scope="col" className="py-1 font-normal">Prioridade</th>
                  <th scope="col" className="py-1 font-normal">Atendente</th>
                  <th scope="col" className="py-1 text-right font-normal">Última mensagem</th>
                </tr>
              </thead>
              <tbody>
                {r.data.items.map((t) => (
                  <tr key={t.id} className="border-t border-steel/60">
                    <td className="tabular py-1.5 text-silver/70">#{t.id}</td>
                    <td className="py-1.5">
                      <Link href={`/admin/chamados/${t.id}`} className="text-data-name hover:underline">
                        {t.subject}
                      </Link>
                    </td>
                    <td className="py-1.5 text-silver/80">{t.gameLogin}</td>
                    <td className="py-1.5 text-silver/80">{ticketCategories[t.category] ?? t.category}</td>
                    <td className={`py-1.5 ${statusClass[t.status] ?? ""}`}>{ticketStatuses[t.status] ?? t.status}</td>
                    <td className={`py-1.5 ${t.priority === "alta" ? "text-data-bad" : "text-silver/70"}`}>{ticketPriorities[t.priority] ?? t.priority}</td>
                    <td className="py-1.5 text-silver/70">{t.assignedName ?? "—"}</td>
                    <td className="tabular py-1.5 text-right text-silver/60">
                      {formatDateTime(t.updatedUtc)} <span className={t.lastAuthor === "jogador" ? "text-data-name" : "text-silver/50"}>({t.lastAuthor})</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {r.ok ? <Pager page={r.data.page} pageSize={r.data.pageSize} total={r.data.total} href={(p) => href({ p })} /> : null}
      </GameWindow>
    </>
  );
}

export default function AdminTickets({ searchParams }: PageProps<"/admin/chamados">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
