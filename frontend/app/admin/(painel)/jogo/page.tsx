import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { requireStaff } from "@/features/admin/session";
import type { GameFunctionSummary } from "@/features/admin/types";
import { Notice, PageTitle, Pill } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Lojas, drops e itens" };

const modeLabel = {
  edit: "Editor",
  insert: "Envio",
  readonly: "Consulta",
} as const;

/** Funções que, sem tabela configurada, abrem a consulta lida dos arquivos do servidor. */
const fromFiles: Record<string, { href: string; note: string }> = {
  "item-catalog": {
    href: "/admin/jogo/catalogo",
    note: "GameServer\\OpenItem + fotos do cliente",
  },
  "monster-drops": {
    href: "/admin/jogo/drops",
    note: "GameServer\\Monster\\*.inf",
  },
};

/** Funções que ficam desligadas de propósito neste servidor, com o motivo. */
const offOnPurpose: Record<string, string> = {
  "box-send":
    "Desligado de propósito: as Box Donate saem pelo sistema de donate do servidor, que monta o item com checksum.",
  "npc-shops":
    "As lojas NPC ficam nos arquivos da pasta GameServer\\NPC, não no banco.",
};

async function Body() {
  await connection();
  const { token } = await requireStaff();
  const r = await apiSend<{
    enabled: boolean;
    functions: GameFunctionSummary[];
  }>("GET", "/api/v1/admin/game-tables", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  return (
    <div className="flex flex-col gap-4">
      {!r.data.enabled ? (
        <Notice title="Funções do jogo desligadas">
          O login de administração do jogo não está configurado na VPS. Veja
          docs/ADMIN.md, seção &quot;Ligar as funções do jogo&quot;.
        </Notice>
      ) : null}
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {r.data.functions.map((f) => {
          const file = !f.configured ? fromFiles[f.key] : undefined;
          const href = file?.href ?? `/admin/jogo/${f.key}`;
          return (
            <div
              key={f.key}
              className="flex flex-col justify-between gap-3 rounded-md border border-steel/70 bg-night/50 p-4 transition-colors hover:border-bronze/70"
            >
              <div>
                <div className="flex items-center justify-between gap-2">
                  <Link
                    href={href}
                    className="text-silver hover:text-gold-bright"
                  >
                    {f.label}
                  </Link>
                  <Pill tone={f.configured || file ? "ok" : "warn"}>
                    {f.configured
                      ? modeLabel[f.mode]
                      : file
                        ? "Arquivos do servidor"
                        : "Não configurado"}
                  </Pill>
                </div>
                <p className="mt-1 text-xs text-silver/60">{f.description}</p>
                {f.source ? (
                  <p className="mt-2 font-mono text-[0.7rem] text-silver/45">
                    {f.source}
                  </p>
                ) : null}
                {file ? (
                  <p className="mt-2 font-mono text-[0.7rem] text-silver/45">
                    {file.note} (consulta)
                  </p>
                ) : null}
                {!f.configured && !file && offOnPurpose[f.key] ? (
                  <p className="mt-2 text-[0.7rem] text-silver/50">
                    {offOnPurpose[f.key]}
                  </p>
                ) : null}
              </div>
              <div className="flex gap-3 text-sm">
                <Link href={href} className="text-arcane hover:underline">
                  Abrir
                </Link>
                {f.canConfigure ? (
                  <Link
                    href={`/admin/jogo/${f.key}/configurar`}
                    className="text-silver/60 hover:text-gold"
                  >
                    Configurar
                  </Link>
                ) : null}
              </div>
            </div>
          );
        })}
      </div>
      <p className="text-xs text-silver/50">
        Estas tabelas ainda não estão no mapeamento do banco. O
        superadministrador liga cada função à tabela real uma vez, em
        &quot;Configurar&quot;; o painel confere a tabela e as colunas a cada
        uso e nunca adivinha nomes.
      </p>
    </div>
  );
}

export default function GameHubPage() {
  return (
    <>
      <PageTitle title="Lojas, drops e itens">
        Preços da Box Donate, loja de Skin, loja de minutos, drops de monstros,
        lojas NPC, catálogo e logs de itens.
      </PageTitle>
      <Suspense
        fallback={<p className="text-sm text-silver/60">Carregando…</p>}
      >
        <Body />
      </Suspense>
    </>
  );
}
