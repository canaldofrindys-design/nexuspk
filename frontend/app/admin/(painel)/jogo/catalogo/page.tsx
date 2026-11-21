import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { ItemCatalogView } from "@/features/admin/GameFileViews";
import { requireStaff } from "@/features/admin/session";
import type { ItemCatalog } from "@/features/admin/types";
import { Notice, PageTitle } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Catálogo de itens" };

async function Body() {
  await connection();
  const { token } = await requireStaff();
  const r = await apiSend<ItemCatalog>("GET", "/api/v1/admin/items", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  if (r.data.items.length === 0)
    return <Notice tone="warn">Não consegui ler os itens do servidor. Confira a pasta C:\ServidorPT\GameServer\OpenItem e a permissão de leitura (docs/INSTALLATION.md, GameServer.ItemsFolder).</Notice>;
  return (
    <GameWindow title="Itens do servidor">
      {r.data.images === 0 ? (
        <p className="mb-3 text-xs text-data-bad">Sem fotos: copie as imagens do cliente para C:\ServidorPT\ItemImages (docs/INSTALLATION.md, GameServer.ItemImagesFolder).</p>
      ) : null}
      <ItemCatalogView items={r.data.items} />
    </GameWindow>
  );
}

export default function ItemCatalogPage() {
  return (
    <>
      <PageTitle title="Catálogo de itens">Todos os itens do servidor, com nome e foto, lidos direto dos arquivos do jogo (pasta OpenItem). Só consulta.</PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </>
  );
}
