import { Suspense } from "react";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { BannerCarousel } from "@/features/home/BannerCarousel";
import { FeatureCards } from "@/features/home/FeatureCards";
import { HeroBanner } from "@/features/home/HeroBanner";
import { HomeChapters } from "@/features/home/HomeChapters";
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
import { KitIcon } from "@/components/ui/KitIcon";
import { connection } from "next/server";

/**
 * Home em quatro partes, cada uma com o seu cenário no fundo (o fundo troca com esmaecer ao rolar):
 * 1) banner animado com três cenas (vale ao entardecer, luar e estrada);
 * 2) a prancha "home" do kit visual: banner de boas-vindas (ou os banners do painel) com
 *    Download / Cadastre-se / Começar a jogar, faixa de seis cards, ranking, patentes, bosses,
 *    classes, guerra de clãs e status do servidor;
 * 3) quatro capítulos ilustrados (primeiros passos, eventos e PvP, comunidade, status e conta);
 * 4) o que está acontecendo agora: avisos, agenda e top 10, com dados ao vivo.
 * No celular tudo vira uma coluna, na mesma ordem.
 */
async function Portal() {
  await connection();
  const home = await loadHome();
  return (
    <div id="portal" data-scene="vale-dourado" className="mx-auto flex max-w-7xl scroll-mt-20 flex-col gap-4 px-4 pt-6 pb-10">
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
    </div>
  );
}

async function LiveBoard() {
  await connection();
  const home = await loadHome();
  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_340px] lg:items-start">
      <NoticeBoard news={home?.news ?? []} events={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]} />
      <AgendaWindow active={home?.activeEvents ?? []} upcoming={home?.upcomingEvents ?? []} />
      <RankingWindow players={home?.topPlayers ?? []} available={home?.rankingAvailable ?? false} />
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
      <HeroBanner />
      <Suspense fallback={<PortalFallback />}>
        <Portal />
      </Suspense>

      <div className="gold-rule mx-auto max-w-5xl" aria-hidden />
      <HomeChapters />

      <section data-scene="vila-conversa" aria-labelledby="agora-title" className="relative py-14 sm:py-20">
        <div className="mx-auto max-w-7xl px-4">
          <div data-reveal="up" className="mb-6 text-center">
            <p className="flex items-center justify-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase">
              <KitIcon name="eventos" size={32} />
              Atualizado a cada visita
            </p>
            <h2 id="agora-title" className="text-gilded mt-2 font-display text-3xl font-bold sm:text-4xl">
              Acontecendo no NexusPK
            </h2>
            <p className="mx-auto mt-2 max-w-2xl text-silver/80">Os avisos da equipe, a agenda dos próximos eventos e os dez personagens de nível mais alto, direto do servidor.</p>
          </div>
          <Suspense fallback={<div className="game-window h-64 animate-pulse" aria-label="Carregando avisos, agenda e ranking" />}>
            <LiveBoard />
          </Suspense>
        </div>
      </section>
    </>
  );
}
