import { LayoutDashboard, UserPlus } from "lucide-react";
import { Suspense } from "react";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { HeroScene } from "@/components/home/HeroScene";
import { AccountWindow } from "@/features/home/AccountWindow";
import { AgendaWindow } from "@/features/home/AgendaWindow";
import { ClassesWindow } from "@/features/home/ClassesWindow";
import { NoticeBoard } from "@/features/home/NoticeBoard";
import { RankingWindow } from "@/features/home/RankingWindow";
import { ServerSheet } from "@/features/home/ServerSheet";
import { StatusWindow } from "@/features/home/StatusWindow";
import { loadHome } from "@/features/home/loadHome";
import { connection } from "next/server";

async function HeroStatus() {
  await connection();
  const home = await loadHome();
  const availability = home?.status.availability ?? "unknown";
  const text = { online: "Servidor online", offline: "Servidor offline", partial: "Servidor parcialmente fora do ar", unknown: "Status em verificação" }[availability];
  const dot = { online: "bg-data-ok", offline: "bg-data-bad", partial: "bg-data-name", unknown: "bg-silver/50" }[availability];
  return (
    <p className="flex items-center gap-2 text-sm text-silver">
      <span aria-hidden className={`inline-block size-2.5 rounded-full ${dot}`} />
      {text}
    </p>
  );
}

async function PortalBoard() {
  await connection();
  const home = await loadHome();
  return (
    <>
      <div className="grid gap-4 lg:grid-cols-[260px_1fr_340px]">
        <div className="flex flex-col gap-4">
          <AccountWindow />
          <StatusWindow status={home?.status ?? null} />
        </div>
        <div className="flex flex-col gap-4">
          <NoticeBoard news={home?.news ?? []} events={[...(home?.activeEvents ?? []), ...(home?.upcomingEvents ?? [])]} />
          <AgendaWindow active={home?.activeEvents ?? []} upcoming={home?.upcomingEvents ?? []} />
        </div>
        <div className="flex flex-col gap-4">
          <RankingWindow players={home?.topPlayers ?? []} available={home?.rankingAvailable ?? false} />
          <ClassesWindow />
        </div>
      </div>
      {home && <ServerSheet server={home.server} />}
    </>
  );
}

function BoardFallback() {
  return <div className="game-window h-64 animate-pulse" aria-label="Carregando informações do servidor" />;
}

export default function HomePage() {
  return (
    <>
      <HeroScene>
        <h1 className="font-display text-5xl text-gold-bright drop-shadow-[0_0_24px_rgb(217_178_106/0.35)] sm:text-7xl">NexusPK</h1>
        <p className="mt-2 max-w-xl text-lg text-silver">Servidor privado de Priston Tale.</p>
        <div className="mt-3">
          <Suspense fallback={<p className="text-sm text-silver/60">Verificando o servidor…</p>}>
            <HeroStatus />
          </Suspense>
        </div>
        <div className="mt-6 flex flex-wrap gap-3">
          <ButtonLink href="/criar-conta" size="lg" icon={<UserPlus className="size-5" aria-hidden />}>
            Criar conta
          </ButtonLink>
          <ButtonLink href="/painel" size="lg" variant="ghost" icon={<LayoutDashboard className="size-5" aria-hidden />}>
            Painel do jogador
          </ButtonLink>
        </div>
      </HeroScene>

      <div className="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-8">
        <Suspense fallback={<BoardFallback />}>
          <PortalBoard />
        </Suspense>
      </div>
    </>
  );
}
