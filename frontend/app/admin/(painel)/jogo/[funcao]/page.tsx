import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { ConfirmDialog, RowDeleteForm, RowEditForm, RowInsertForm } from "@/features/admin/gameForms";
import { requireStaff } from "@/features/admin/session";
import { ItemThumb } from "@/features/admin/ItemPicker";
import type { BrowseView, CatalogItem, GameColumnView, GameFunctionSummary, GameOk, ItemCatalog } from "@/features/admin/types";
import { Notice, PageTitle } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Tabela do jogo" };

type Q = Record<string, string | string[] | undefined>;

async function Body({ params, searchParams }: { params: Promise<{ funcao: string }>; searchParams: Promise<Q> }) {
  await connection();
  const fn = (await params).funcao;
  const q = await searchParams;
  const { me, token } = await requireStaff();
  const page = Math.max(1, Number.parseInt(String(q.pagina ?? "1"), 10) || 1);
  const filters: Record<string, string> = {};
  for (const [k, v] of Object.entries(q)) if (k.startsWith("f.") && typeof v === "string" && v.trim()) filters[k] = v.slice(0, 200);
  const [r, cat] = await Promise.all([
    apiSend<GameOk<BrowseView>>("GET", `/api/v1/admin/game-tables/${encodeURIComponent(fn)}?${new URLSearchParams({ ...filters, pagina: String(page) })}`, { token }),
    apiSend<ItemCatalog>("GET", "/api/v1/admin/items", { token }),
  ]);
  const catalog: CatalogItem[] = cat.ok ? cat.data.items : [];
  const byId = new Map(catalog.map((i) => [i.id, i]));

  if (!r.ok) {
    const list = await apiSend<{ functions: GameFunctionSummary[] }>("GET", "/api/v1/admin/game-tables", { token });
    const f = list.ok ? list.data.functions.find((x) => x.key === fn) : undefined;
    return (
      <>
        <PageTitle title={f?.label ?? "Tabela do jogo"}>{f?.description}</PageTitle>
        <Notice tone={r.error.code === "disabled" ? "warn" : "bad"}>
          {r.error.errors ? Object.values(r.error.errors).join(" ") : r.error.message}
          {f?.canConfigure && r.error.code === "disabled" ? (
            <>
              {" "}
              <Link href={`/admin/jogo/${fn}/configurar`} className="text-arcane hover:underline">
                Configurar agora
              </Link>
            </>
          ) : null}
        </Notice>
      </>
    );
  }

  const d = r.data.data;
  const shown = d.columns.filter((c) => c.supported);
  const search = d.columns.filter((c) => c.isSearch);
  const isSend = d.function.mode === "insert";
  const href = (p: number) => `/admin/jogo/${fn}?${new URLSearchParams({ ...filters, pagina: String(p) })}`;
  const canConfigure = me.capabilities.includes("ConfigureGameSources");
  const itemCols = itemColumns(d.columns, d.rows);
  const values = d.rows.flatMap((row) => itemCols.map((c) => row[c] ?? "")).filter(Boolean);
  const lowercase = values.length === 0 || values.filter((x) => x === x.toLowerCase()).length * 2 >= values.length;
  const items = itemCols.length > 0 ? { columns: itemCols, catalog, lowercase } : undefined;

  return (
    <>
      <PageTitle
        title={d.function.label}
        actions={
          canConfigure ? (
            <Link href={`/admin/jogo/${fn}/configurar`} className="rounded-sm border border-steel px-3 py-1.5 text-sm text-silver/70 hover:border-arcane hover:text-arcane">
              Configurar
            </Link>
          ) : null
        }
      >
        {d.function.description} <span className="font-mono text-xs text-silver/45">({d.function.source})</span>
      </PageTitle>

      <div className="flex flex-col gap-4">
        {d.canInsert ? (
          <GameWindow title={isSend ? "Enviar" : "Incluir linha"}>
            {isSend ? (
              <RowInsertForm fn={fn} columns={d.columns} items={items} submitLabel="Enviar para a conta" accountHint="O painel confere se a conta existe antes de enviar." />
            ) : (
              <ConfirmDialog label="Incluir linha" title={`Incluir em ${d.function.label}`} small>
                <RowInsertForm fn={fn} columns={d.columns} items={items} submitLabel="Incluir" />
              </ConfirmDialog>
            )}
          </GameWindow>
        ) : null}

        <GameWindow title={isSend ? "Últimos envios" : "Linhas"}>
          {search.length > 0 ? (
            <form method="get" className="mb-3 flex flex-wrap items-end gap-2 text-sm">
              {search.map((c) => (
                <label key={c.name} className="flex flex-col gap-1">
                  <span className="text-xs text-silver/60">{c.name}</span>
                  <input
                    name={`f.${c.name}`}
                    defaultValue={typeof q[`f.${c.name}`] === "string" ? (q[`f.${c.name}`] as string) : ""}
                    maxLength={200}
                    className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1.5"
                  />
                </label>
              ))}
              <button type="submit" className="rounded-sm border border-bronze px-3 py-1.5 text-gold hover:border-gold">
                Procurar
              </button>
              <span className="text-xs text-silver/45">Texto procura pelo começo; use = na frente para valor exato.</span>
            </form>
          ) : null}

          {d.rows.length === 0 ? (
            <p className="text-sm text-silver/60">Nenhuma linha.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm" style={{ minWidth: `${Math.min(shown.length * 120 + 140, 2400)}px` }}>
                <thead>
                  <tr className="text-left text-xs text-silver/55">
                    {shown.map((c) => (
                      <th key={c.name} scope="col" className="whitespace-nowrap py-1 pr-3 font-normal">
                        {c.name}
                        {c.isKey ? <span className="ml-1 text-gold/70" title="chave">●</span> : null}
                      </th>
                    ))}
                    {d.canEdit || d.canDelete ? (
                      <th scope="col" className="py-1 font-normal">
                        <span className="sr-only">Ações</span>
                      </th>
                    ) : null}
                  </tr>
                </thead>
                <tbody>
                  {d.rows.map((row, i) => (
                    <tr key={i} className="border-t border-steel/60 align-top hover:bg-steel/20">
                      {shown.map((c) => (
                        <td key={c.name} className={`max-w-xs truncate py-1.5 pr-3 ${c.isKey ? "text-data-name" : "text-silver/80"}`} title={row[c.name] ?? "NULL"}>
                          {row[c.name] == null ? (
                            <span className="text-silver/35">NULL</span>
                          ) : itemCols.includes(c.name) && byId.get(row[c.name]!.trim().toUpperCase()) ? (
                            <ItemCell value={row[c.name]!} item={byId.get(row[c.name]!.trim().toUpperCase())!} />
                          ) : (
                            row[c.name]
                          )}
                        </td>
                      ))}
                      {d.canEdit || d.canDelete ? (
                        <td className="py-1.5">
                          <span className="flex justify-end gap-1.5">
                            {d.canEdit ? (
                              <ConfirmDialog label="Editar" title="Editar linha" small tone="calm">
                                <RowEditForm fn={fn} columns={d.columns} row={row} />
                              </ConfirmDialog>
                            ) : null}
                            {d.canDelete ? (
                              <ConfirmDialog label="Apagar" title="Apagar linha" small tone="danger">
                                <RowDeleteForm fn={fn} columns={d.columns} row={row} />
                              </ConfirmDialog>
                            ) : null}
                          </span>
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="mt-3 flex items-center justify-between text-sm">
            {d.page > 1 ? (
              <Link href={href(d.page - 1)} className="text-arcane hover:underline">
                ← Anterior
              </Link>
            ) : (
              <span />
            )}
            <span className="text-xs text-silver/50">Página {d.page}</span>
            {d.hasMore ? (
              <Link href={href(d.page + 1)} className="text-arcane hover:underline">
                Próxima →
              </Link>
            ) : (
              <span />
            )}
          </div>
        </GameWindow>
      </div>
    </>
  );
}

const itemCodePattern = /^[A-Za-z]{2}[0-9]{3,4}$/;

/** Colunas com código de item: pelo nome (ItemCode, szItemCode, CodeIT) ou porque os valores têm cara de código ("ca105"). */
function itemColumns(columns: GameColumnView[], rows: Record<string, string | null>[]) {
  return columns
    .filter((c) => ["char", "varchar", "nchar", "nvarchar"].includes(c.type))
    .filter((c) => {
      if (/item.?code|code.?it/i.test(c.name)) return true;
      const vals = rows.map((r) => r[c.name]).filter((x): x is string => !!x);
      return vals.length > 0 && vals.filter((x) => itemCodePattern.test(x.trim())).length * 2 > vals.length;
    })
    .map((c) => c.name);
}

function ItemCell({ value, item }: { value: string; item: CatalogItem }) {
  return (
    <span className="flex items-center gap-2">
      <ItemThumb id={item.id} image={item.image} />
      <span className="min-w-0">
        <span className="block truncate text-data-name">{item.name ?? value}</span>
        <span className="block font-mono text-[11px] text-silver/50">{value}</span>
      </span>
    </span>
  );
}

export default function GameTablePage({ params, searchParams }: PageProps<"/admin/jogo/[funcao]">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body params={params} searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
