import { Suspense } from "react";
import { AccountWindow } from "@/features/home/AccountWindow";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { HomeHighlight } from "@/features/home/HomeHighlight";
import { NoticeBoard } from "@/features/home/NoticeBoard";
import { RankingWindow } from "@/features/home/RankingWindow";
import { ServerSheet } from "@/features/home/ServerSheet";
import { StatusWindow } from "@/features/home/StatusWindow";
import { loadHome } from "@/features/home/loadHome";
import { connection } from "next/server";

/**
 * Home em formato de portal: destaque curto e, abaixo, três colunas de janelas.
 * No celular as colunas viram uma lista na ordem: conta, avisos, agenda, ranking, servidor.
 */
async function Portal() {
  await connection();
  const home = await loadHome();
  return (
    <>
      <HomeHighlight home={home} />
      <div className="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-6">
        <div className="flex flex-col gap-4 lg:grid lg:grid-cols-[270px_1fr_320px] lg:items-start">
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className="order-1 lg:order-none">
              <AccountWindow />
            </div>
            <div className="order-5 lg:order-none">
              <StatusWindow status={home?.status ?? null} />
            </div>
          </div>
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className="order-2 lg:order-none">
              <NoticeBoard news={home?.news ?? []} events={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]} />
            </div>
            <div className="order-3 lg:order-none">
              <AgendaWindow active={home?.activeEvents ?? []} upcoming={home?.upcomingEvents ?? []} />
            </div>
          </div>
          <div className="order-4 lg:order-none">
            <RankingWindow players={home?.topPlayers ?? []} available={home?.rankingAvailable ?? false} />
          </div>
        </div>
        {home && <ServerSheet server={home.server} />}
      </div>
    </>
  );
}

function PortalFallback() {
  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <div className="game-window h-64 animate-pulse" aria-label="Carregando informações do servidor" />
    </div>
  );
}

export default function HomePage() {
  return (
    <Suspense fallback={<PortalFallback />}>
      <Portal />
    </Suspense>
  );
}
