import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { clientContext, getSessionToken } from "@/features/auth/session";
import { Pager } from "@/features/ranking/Pager";
import { PlayerTicketTable } from "@/features/support/PlayerTickets";
import type { TicketList } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Meus chamados", robots: { index: false } };

type Q = { pagina?: string };

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const token = await getSessionToken();
  if (!token) redirect("/login?next=/painel/suporte");
  const page = Math.max(1, Number.parseInt((await searchParams).pagina ?? "1", 10) || 1);
  const r = await apiSend<TicketList>("GET", `/api/v1/player/tickets?pagina=${page}`, { token, ...(await clientContext()) });
  if (!r.ok && r.status === 401) redirect("/login?next=/painel/suporte");
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  return r.data.items.length === 0 ? (
    <p className="text-sm text-silver/70">Você ainda não abriu nenhum chamado.</p>
  ) : (
    <>
      <PlayerTicketTable items={r.data.items} />
      <Pager page={r.data.page} pageSize={r.data.pageSize} total={r.data.total} href={(p) => `/painel/suporte?pagina=${p}`} />
    </>
  );
}

export default function PlayerTicketsPage({ searchParams }: PageProps<"/painel/suporte">) {
  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-10">
      <p className="mb-2 text-sm">
        <Link href="/painel" className="text-arcane hover:underline">
          Painel do jogador
        </Link>
      </p>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl text-gold-bright">Meus chamados</h1>
        <Link href="/painel/suporte/novo" className="border border-gold bg-gold/15 px-3 py-1.5 text-sm text-gold-bright hover:bg-gold/25">
          Abrir chamado
        </Link>
      </div>
      <GameWindow title="Chamados">
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
          <Body searchParams={searchParams as Promise<Q>} />
        </Suspense>
      </GameWindow>
      <p className="mt-3 text-xs text-silver/60">
        Cada conta pode ter até 3 chamados em andamento. Antes de abrir um chamado sobre o servidor fora do ar, confira a{" "}
        <Link href="/status" className="text-arcane hover:underline">
          página de status
        </Link>
        .
      </p>
    </div>
  );
}
