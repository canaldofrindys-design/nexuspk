import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { NpcShopsView } from "@/features/admin/GameFileViews";
import { requireStaff } from "@/features/admin/session";
import type { NpcShopList } from "@/features/admin/types";
import { Notice, PageTitle } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Lojas NPC" };

async function Body() {
  await connection();
  const { token } = await requireStaff();
  const r = await apiSend<NpcShopList>("GET", "/api/v1/admin/npc-shops", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  if (!r.data.available || r.data.shops.length === 0)
    return (
      <Notice tone="warn">
        Não consegui ler as lojas dos NPCs. Confira a pasta C:\ServidorPT\GameServer\NPC (a permissão de leitura é liberada sozinha na próxima atualização do portal; veja
        docs/INSTALLATION.md, GameServer.NpcFolder).
      </Notice>
    );
  return (
    <GameWindow title="Lojas dos NPCs">
      <NpcShopsView shops={r.data.shops} />
    </GameWindow>
  );
}

export default function NpcShopsPage() {
  return (
    <>
      <PageTitle title="Lojas NPC">
        O que cada NPC vende, lido direto dos arquivos do servidor (pasta NPC). Só consulta: para mudar uma loja, edite as linhas de venda do arquivo .npc e reinicie o servidor do
        jogo.
      </PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </>
  );
}
