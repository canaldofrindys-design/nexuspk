import { Suspense } from "react";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { BannerCarousel } from "@/features/home/BannerCarousel";
import { ActionButtons, FeatureCards } from "@/features/home/FeatureCards";
import { HeroBanner } from "@/features/home/HeroBanner";
import { defaultSchedule } from "@/features/home/eventSchedule";
import { HomeChapters } from "@/features/home/HomeChapters";
import { NoticeBoard } from "@/features/home/NoticeBoard";
import {
  BattlePointsPanel,
  LeadersColumn,
  ServerEventsWindow,
} from "@/features/home/Highlights";
import {
  LiveTilesRow,
  PvpLadderRow,
  RareDropsRow,
} from "@/features/home/HeroJourney";
import {
  ClassesStrip,
  ServerStatusPanel,
} from "@/features/home/PortalSections";
import { loadHome } from "@/features/home/loadHome";
import { KitIcon } from "@/components/ui/KitIcon";
import { connection } from "next/server";

/**
 * Home em quatro partes, cada uma com o seu cenário no fundo (o fundo troca com esmaecer ao rolar):
 * 1) banner grande e animado: os eventos principais (Bless Castle e os horários fixos do painel) com
 *    contagem regressiva até o início, depois três cenas (vale ao entardecer, luar e estrada);
 * 2) a prancha "portal" do kit visual: banners do painel (quando houver), faixa de cinco banners,
 *    faixa de botões e o quadro em três colunas (ranking de BP e status | classes e a "Jornada do Herói":
 *    drops raros da semana, elos PvP e cards ao vivo | líder do Bless Castle, líder do SoD e bandeiras);
 * 3) quatro capítulos ilustrados (primeiros passos, eventos e PvP, comunidade, status e conta);
 * 4) o que está acontecendo agora: avisos, agenda e os eventos fixos do servidor.
 * No celular tudo vira uma coluna, na mesma ordem.
 */
async function Portal() {
  await connection();
  const home = await loadHome();
  return (
    <div
      id="portal"
      data-scene="vale-dourado"
      className="mx-auto flex max-w-7xl scroll-mt-20 flex-col gap-4 px-4 pt-6 pb-10"
    >
      {home?.banners?.length ? (
        <div data-reveal="zoom">
          <BannerCarousel banners={home.banners} />
        </div>
      ) : null}

      <FeatureCards />
      <ActionButtons />

      {/* As três colunas terminam na mesma linha, como na prancha: o centro e a direita distribuem o espaço. */}
      <div className="nx-board grid gap-4 lg:grid-cols-[250px_minmax(0,1fr)_250px] lg:items-stretch">
        <div className="flex flex-col gap-4">
          <BattlePointsPanel home={home} />
          <ServerStatusPanel home={home} />
        </div>
        <div className="flex min-w-0 flex-col justify-between gap-4">
          <ClassesStrip />
          <RareDropsRow home={home} />
          <PvpLadderRow home={home} />
          <LiveTilesRow home={home} />
        </div>
        <LeadersColumn home={home} />
      </div>
    </div>
  );
}

async function LiveBoard() {
  await connection();
  const home = await loadHome();
  return (
    <div className="grid gap-4 lg:grid-cols-3 lg:items-start">
      <ServerEventsWindow />
      <NoticeBoard
        news={home?.news ?? []}
        events={[
          ...(home?.activeEvents ?? []),
          ...(home?.upcomingEvents ?? []),
        ]}
      />
      <AgendaWindow
        active={home?.activeEvents ?? []}
        upcoming={home?.upcomingEvents ?? []}
      />
    </div>
  );
}

/** Banner com os horários do painel e a agenda; até a API responder, o Bless Castle padrão. */
async function Hero() {
  await connection();
  const home = await loadHome();
  return (
    <HeroBanner
      schedule={home?.schedule ?? defaultSchedule}
      agenda={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]}
      initialNow={new Date().getTime()}
    />
  );
}

function PortalFallback() {
  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <div
        className="game-window h-64 animate-pulse"
        aria-label="Carregando informações do servidor"
      />
    </div>
  );
}

export default function HomePage() {
  return (
    <>
      <h1 className="sr-only">NexusPK, servidor privado de Priston Tale</h1>
      <Suspense fallback={<HeroBanner schedule={defaultSchedule} />}>
        <Hero />
      </Suspense>
      <Suspense fallback={<PortalFallback />}>
        <Portal />
      </Suspense>

      <div className="gold-rule mx-auto max-w-5xl" aria-hidden />
      <HomeChapters />

      <section
        data-scene="vila-conversa"
        aria-labelledby="agora-title"
        className="relative py-14 sm:py-20"
      >
        <div className="mx-auto max-w-7xl px-4">
          <div data-reveal="up" className="mb-6 text-center">
            <p className="flex items-center justify-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase">
              <KitIcon name="eventos" size={32} />
              Atualizado a cada visita
            </p>
            <h2
              id="agora-title"
              className="text-gilded mt-2 font-display text-3xl font-bold sm:text-4xl"
            >
              Acontecendo no NexusPK
            </h2>
            <p className="mx-auto mt-2 max-w-2xl text-silver/80">
              Os eventos do servidor, os avisos da equipe e a agenda dos
              próximos eventos.
            </p>
          </div>
          <Suspense
            fallback={
              <div
                className="game-window h-64 animate-pulse"
                aria-label="Carregando avisos e agenda"
              />
            }
          >
            <LiveBoard />
          </Suspense>
        </div>
      </section>
    </>
  );
}
