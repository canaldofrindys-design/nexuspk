import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { ConfirmDialog, SourceConfigForm, SourceRemoveForm } from "@/features/admin/gameForms";
import { requireStaff } from "@/features/admin/session";
import type { GameColumnView, GameOk, SourceConfigPage, TableSuggestion } from "@/features/admin/types";
import { Notice, PageTitle, Pill } from "@/features/admin/ui";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Configurar função" };

const selectClass = "min-w-56 rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm";

const clean = (v?: string) => (typeof v === "string" && /^[A-Za-z0-9_]{1,128}$/.test(v) ? v : undefined);

async function Body({ params, searchParams }: { params: Promise<{ funcao: string }>; searchParams: Promise<{ banco?: string; tabela?: string }> }) {
  await connection();
  const fn = (await params).funcao;
  const sp = await searchParams;
  const banco = clean(sp.banco);
  const tabela = clean(sp.tabela);
  const { token } = await requireStaff("ConfigureGameSources");
  const base = `/api/v1/admin/game-tables/${encodeURIComponent(fn)}/config`;
  const r = await apiSend<SourceConfigPage>("GET", base, { token });
  if (!r.ok) return <Notice tone="bad">{r.status === 404 ? "Função desconhecida." : r.error.message}</Notice>;
  const d = r.data;
  const db = banco ?? d.binding?.database;
  const table = tabela ?? (db === d.binding?.database ? d.binding?.table : undefined);

  const tables = db ? await apiSend<GameOk<TableSuggestion[]>>("GET", `${base}/tables?${new URLSearchParams({ banco: db })}`, { token }) : null;
  const columns = db && table ? await apiSend<GameOk<GameColumnView[]>>("GET", `${base}/columns?${new URLSearchParams({ banco: db, tabela: table })}`, { token }) : null;

  return (
    <>
      <PageTitle
        title={`Configurar: ${d.function.label}`}
        actions={
          d.binding ? (
            <ConfirmDialog label="Desligar função" title="Desligar função" tone="danger" small>
              <SourceRemoveForm fn={fn} />
            </ConfirmDialog>
          ) : null
        }
      >
        {d.function.description}{" "}
        <Link href={`/admin/jogo/${fn}`} className="text-arcane hover:underline">
          Abrir a função
        </Link>
      </PageTitle>

      <div className="flex flex-col gap-4">
        {d.binding ? (
          <Notice tone="info" title="Configuração atual">
            <span className="font-mono">
              {d.binding.database}.dbo.{d.binding.table}
            </span>{" "}
            · chave: {d.binding.keyColumns.join(", ") || "—"} · alterada por {d.binding.updatedBy} em {formatDateTime(d.binding.updatedUtc)}
          </Notice>
        ) : null}
        {d.message ? <Notice>{d.message}</Notice> : null}

        <GameWindow title="1. Banco e tabela do jogo">
          <form method="get" className="flex flex-wrap items-end gap-3 text-sm">
            <label className="flex flex-col gap-1">
              <span className="text-xs text-silver/60">Banco</span>
              <select name="banco" defaultValue={db ?? ""} className={selectClass}>
                <option value="">Escolha…</option>
                {(d.databases ?? []).map((name) => (
                  <option key={name} value={name}>
                    {name}
                    {d.function.suggestedDatabases.includes(name) ? " (sugerido)" : ""}
                  </option>
                ))}
              </select>
            </label>
            {tables?.ok ? (
              <label className="flex flex-col gap-1">
                <span className="text-xs text-silver/60">Tabela</span>
                <select name="tabela" defaultValue={table ?? ""} className={selectClass}>
                  <option value="">Escolha…</option>
                  {tables.data.data.map((t) => (
                    <option key={t.name} value={t.name}>
                      {t.suggested ? "★ " : ""}
                      {t.name} ({t.rows.toLocaleString("pt-BR")} linhas)
                    </option>
                  ))}
                </select>
              </label>
            ) : null}
            <button type="submit" className="rounded-sm border border-bronze px-3 py-2 text-gold hover:border-gold">
              {tables?.ok ? "Ver colunas" : "Ver tabelas"}
            </button>
          </form>
          {tables && !tables.ok ? <p className="mt-2 text-sm text-data-bad">{tables.error.message}</p> : null}
          <p className="mt-2 text-xs text-silver/50">★ = o nome parece com esta função. Confira com quem conhece o servidor antes de salvar.</p>
        </GameWindow>

        {columns ? (
          <GameWindow title="2. Colunas">
            {!columns.ok ? (
              <p className="text-sm text-data-bad">{columns.error.message}</p>
            ) : (
              <>
                <div className="mb-3 flex flex-wrap gap-2 text-xs">
                  <Pill tone="info">Chave: identifica cada linha (ex.: código do item)</Pill>
                  {d.function.mode === "edit" ? <Pill tone="info">Editável: a equipe pode mudar</Pill> : null}
                  <Pill tone="info">Busca: aparece no filtro</Pill>
                  {d.function.mode === "insert" ? <Pill tone="info">Conta: recebe o login do jogador</Pill> : null}
                </div>
                <SourceConfigForm fn={fn} mode={d.function.mode} database={db!} table={table!} columns={columns.data.data} binding={d.binding} />
              </>
            )}
          </GameWindow>
        ) : null}
      </div>
    </>
  );
}

export default function ConfigurePage({ params, searchParams }: PageProps<"/admin/jogo/[funcao]/configurar">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body params={params} searchParams={searchParams as Promise<{ banco?: string; tabela?: string }>} />
    </Suspense>
  );
}
