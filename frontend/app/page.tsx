import { Suspense } from "react";
import { AccountWindow } from "@/features/home/AccountWindow";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { ClassShowcase } from "@/features/home/ClassShowcase";
import { HomeHero } from "@/features/home/HomeHero";
import { QuickBar } from "@/features/home/QuickBar";
import { NoticeBoard } from "@/features/home/NoticeBoard";
import { RankingWindow } from "@/features/home/RankingWindow";
import { ServerSheet } from "@/features/home/ServerSheet";
import { StatusWindow } from "@/features/home/StatusWindow";
import { loadHome } from "@/features/home/loadHome";
import { connection } from "next/server";

/**
 * Home: abertura com o cenário do castelo e, abaixo, três colunas de janelas e a vitrine de classes.
 * No celular: atalhos, avisos, agenda, ranking, conta e servidor (o login desce, porque o visitante
 * já tem "Criar conta" e "Painel" na abertura e "Entrar" no menu).
 */
async function Portal() {
  await connection();
  const home = await loadHome();
  return (
    <>
      <HomeHero home={home} />
      <div id="portal" className="mx-auto flex max-w-7xl scroll-mt-20 flex-col gap-4 px-4 pt-0 pb-6">
        <div className="relative z-10 -mt-4 lg:hidden">
          <QuickBar />
        </div>
        <div className="relative z-10 flex flex-col gap-4 lg:-mt-6 lg:grid lg:grid-cols-[240px_minmax(0,1fr)_300px] xl:grid-cols-[270px_minmax(0,1fr)_320px] lg:items-start">
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className="order-4 lg:order-none">
              <AccountWindow />
            </div>
            <div className="order-5 lg:order-none">
              <StatusWindow status={home?.status ?? null} />
            </div>
          </div>
          <div className="contents lg:flex lg:flex-col lg:gap-4">
            <div className="order-1 lg:order-none">
              <NoticeBoard news={home?.news ?? []} events={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]} />
            </div>
            <div className="order-2 lg:order-none">
              <AgendaWindow active={home?.activeEvents ?? []} upcoming={home?.upcomingEvents ?? []} />
            </div>
          </div>
          <div className="order-3 lg:order-none">
            <RankingWindow players={home?.topPlayers ?? []} available={home?.rankingAvailable ?? false} />
          </div>
        </div>
        <ClassShowcase />
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
