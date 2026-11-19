import { Suspense } from "react";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { BannerCarousel } from "@/features/home/BannerCarousel";
import { FeatureCards } from "@/features/home/FeatureCards";
import { NoticeBoard } from "@/features/home/NoticeBoard";
import {
  BossesCard,
  ClanWarCard,
  ClassesStrip,
  HomeActions,
  RankEmblems,
  RankingLinks,
  ServerStatusPanel,
} from "@/features/home/PortalSections";
import { RankingWindow } from "@/features/home/RankingWindow";
import { WelcomeBanner } from "@/features/home/WelcomeBanner";
import { loadHome } from "@/features/home/loadHome";
import { connection } from "next/server";

/**
 * Home montada na ordem da prancha "home" do kit visual:
 * 1) banner de boas-vindas (ou os banners do painel) com Download / Cadastre-se / Começar a jogar ao lado;
 * 2) faixa de seis cards; 3) Ranking top geral | patentes, Bosses, Classes e Guerra de clãs | Status do servidor.
 * Abaixo da prancha ficam os blocos com dados ao vivo (avisos, agenda e top 10), no mesmo estilo.
 * No celular tudo vira uma coluna, na mesma ordem.
 */
async function Portal() {
  await connection();
  const home = await loadHome();
  return (
    <div id="portal" className="mx-auto flex max-w-7xl flex-col gap-4 px-4 pt-4 pb-8">
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_330px]">
        <div data-reveal="zoom">{home?.banners?.length ? <BannerCarousel banners={home.banners} /> : <WelcomeBanner />}</div>
        <HomeActions />
      </div>

      <FeatureCards />

      <div className="grid gap-4 lg:grid-cols-[240px_minmax(0,1fr)_260px] lg:items-start">
        <RankingLinks />
        <div className="flex flex-col gap-4">
          <RankEmblems />
          <div className="grid grid-cols-2 gap-3 md:grid-cols-[1fr_1.75fr_0.85fr] md:items-stretch">
            <div className="self-center">
              <BossesCard />
            </div>
            <div className="col-span-2 row-start-2 md:col-span-1 md:row-start-auto">
              <ClassesStrip />
            </div>
            <div className="self-center">
              <ClanWarCard />
            </div>
          </div>
        </div>
        <ServerStatusPanel home={home} />
      </div>

      <div className="gold-rule my-2" aria-hidden />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_340px] lg:items-start">
        <NoticeBoard news={home?.news ?? []} events={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]} />
        <AgendaWindow active={home?.activeEvents ?? []} upcoming={home?.upcomingEvents ?? []} />
        <RankingWindow players={home?.topPlayers ?? []} available={home?.rankingAvailable ?? false} />
      </div>
    </div>
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
    <>
      <h1 className="sr-only">NexusPK, servidor privado de Priston Tale</h1>
      <Suspense fallback={<PortalFallback />}>
        <Portal />
      </Suspense>
    </>
  );
}
