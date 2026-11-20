import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import type { AccountHit, GameOk } from "@/features/admin/types";
import { Notice, PageTitle, Pill } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Contas" };

async function Results({ q }: { q: string }) {
  await connection();
  const { token } = await requireStaff("ViewAccounts");
  if (q.trim().length < 2)
    return (
      <p className="text-sm text-silver/60">
        Digite o login da conta ou o nome de um personagem.
      </p>
    );
  const r = await apiSend<GameOk<AccountHit[]>>(
    "GET",
    `/api/v1/admin/accounts?${new URLSearchParams({ q })}`,
    { token },
  );
  if (!r.ok)
    return (
      <Notice tone={r.error.code === "disabled" ? "warn" : "bad"}>
        {r.error.errors?.q ?? r.error.message}
      </Notice>
    );
  if (r.data.data.length === 0)
    return <p className="text-sm text-silver/70">Nenhuma conta encontrada.</p>;
  return (
    <ul className="divide-y divide-steel/60">
      {r.data.data.map((h) => (
        <li key={h.login}>
          <Link
            href={`/admin/contas/${encodeURIComponent(h.login)}`}
            className="flex flex-wrap items-center justify-between gap-2 px-2 py-2.5 hover:bg-steel/25"
          >
            <span>
              <span className="text-data-name">{h.login}</span>
              {h.matchedCharacter ? (
                <span className="ml-2 text-xs text-silver/55">
                  personagem {h.matchedCharacter}
                </span>
              ) : null}
            </span>
            <span className="flex gap-1.5">
              {h.deleted ? <Pill tone="bad">Apagada</Pill> : null}
              {h.blocked ? (
                <Pill tone="bad">Banida</Pill>
              ) : (
                <Pill tone="ok">Liberada</Pill>
              )}
            </span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

async function Search({
  searchParams,
}: {
  searchParams: Promise<{ q?: string }>;
}) {
  const sp = await searchParams;
  const q = typeof sp.q === "string" ? sp.q.slice(0, 32) : "";
  return (
    <>
      <form method="get" className="mb-4 flex flex-wrap gap-2">
        <label htmlFor="q" className="sr-only">
          Login ou personagem
        </label>
        <input
          id="q"
          name="q"
          defaultValue={q}
          maxLength={32}
          autoFocus
          placeholder="Login ou nome do personagem"
          className="min-w-64 flex-1 rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm outline-none focus-visible:border-arcane"
        />
        <button
          type="submit"
          className="nx-btn rounded-sm border px-4 py-2 text-sm font-semibold"
        >
          Procurar
        </button>
      </form>
      <Suspense
        key={q}
        fallback={<p className="text-sm text-silver/60">Procurando…</p>}
      >
        <Results q={q} />
      </Suspense>
    </>
  );
}

export default function AccountsPage({
  searchParams,
}: PageProps<"/admin/contas">) {
  return (
    <>
      <PageTitle title="Contas e banimentos">
        Procure pela conta ou por um personagem para ver detalhes, diagnóstico,
        coins, VIP e banimento.
      </PageTitle>
      <GameWindow title="Procurar conta">
        <Suspense fallback={null}>
          <Search searchParams={searchParams as Promise<{ q?: string }>} />
        </Suspense>
      </GameWindow>
    </>
  );
}
