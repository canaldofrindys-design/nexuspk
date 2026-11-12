import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { GameWindow } from "@/components/ui/GameWindow";
import { Prose } from "@/features/content/Prose";
import { formatDate } from "@/features/home/format";
import { CommunityLinks } from "@/features/site/CommunityLinks";
import { loadSite } from "@/features/site/loadSite";

export const metadata: Metadata = { title: "O Servidor", description: "Informações sobre o servidor NexusPK: história, taxas e comunidade." };

async function Body() {
  await connection();
  const site = await loadSite();
  if (!site) return <p className="text-sm text-data-bad">Informações indisponíveis no momento. Tente de novo em instantes.</p>;
  const { server } = site;
  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start">
      <GameWindow title={`Sobre o ${server.name}`}>
        {server.tagline ? <p className="mb-3 font-display text-lg text-data-name">{server.tagline}</p> : null}
        {server.about ? (
          <div className="space-y-3 text-sm leading-relaxed text-silver/90">
            <Prose text={server.about} />
          </div>
        ) : (
          <p className="text-sm text-silver/70">A apresentação do servidor será publicada pela equipe em breve.</p>
        )}
        {site.serverUpdatedUtc ? <p className="mt-4 text-xs text-silver/50">Atualizado em {formatDate(site.serverUpdatedUtc)}.</p> : null}
      </GameWindow>
      <div className="flex flex-col gap-4">
        <GameWindow title="Taxas" as="aside">
          {server.rates.length === 0 ? (
            <p className="text-sm text-silver/70">Taxas a publicar.</p>
          ) : (
            <dl className="grid grid-cols-[1fr_auto] gap-y-1.5 text-sm">
              {server.rates.map((r) => (
                <div key={r.label} className="contents">
                  <dt className="text-silver/80">{r.label}</dt>
                  <dd className="tabular text-right text-data-info">{r.value}</dd>
                </div>
              ))}
            </dl>
          )}
          <p className="mt-3 text-xs text-silver/60">Horários do site e dos eventos em horário de Brasília.</p>
        </GameWindow>
        <GameWindow title="Comunidade" as="aside">
          {server.communityLinks.length === 0 ? (
            <p className="text-sm text-silver/70">
              Fale com a equipe pela{" "}
              <Link href="/suporte" className="text-arcane hover:underline">
                central de suporte
              </Link>
              .
            </p>
          ) : (
            <CommunityLinks links={server.communityLinks} />
          )}
        </GameWindow>
        <GameWindow title="Começar a jogar" as="aside">
          <div className="flex flex-wrap gap-2">
            <ButtonLink href="/criar-conta">Criar conta</ButtonLink>
            <ButtonLink href="/download" variant="ghost">
              Baixar o jogo
            </ButtonLink>
          </div>
          <p className="mt-3 text-sm text-silver/70">
            Veja também o{" "}
            <Link href="/status" className="text-arcane hover:underline">
              status do servidor
            </Link>{" "}
            e as{" "}
            <Link href="/classes" className="text-arcane hover:underline">
              classes
            </Link>
            .
          </p>
        </GameWindow>
      </div>
    </div>
  );
}

export default function ServerPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">O Servidor</h1>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </div>
  );
}
