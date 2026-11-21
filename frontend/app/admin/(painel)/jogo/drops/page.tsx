import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { MonsterDropsView } from "@/features/admin/GameFileViews";
import { requireStaff } from "@/features/admin/session";
import type { MonsterList } from "@/features/admin/types";
import { Notice, PageTitle } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Drops de monstros" };

async function Body() {
  await connection();
  const { token } = await requireStaff();
  const r = await apiSend<MonsterList>("GET", "/api/v1/admin/monsters", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  if (!r.data.available || r.data.monsters.length === 0)
    return <Notice tone="warn">Não consegui ler os monstros do servidor. Confira a pasta C:\ServidorPT\GameServer\Monster e a permissão de leitura (docs/INSTALLATION.md, GameServer.MonstersFolder).</Notice>;
  return (
    <GameWindow title="Monstros e drops">
      <MonsterDropsView monsters={r.data.monsters} />
    </GameWindow>
  );
}

export default function MonsterDropsPage() {
  return (
    <>
      <PageTitle title="Drops de monstros">
        O que cada monstro deixa cair e a chance, lido direto dos arquivos do servidor (pasta Monster). Só consulta: para mudar um drop, edite o arquivo .inf do monstro e reinicie o
        servidor do jogo.
      </PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </>
  );
}
