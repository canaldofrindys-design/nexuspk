import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { Download, ExternalLink } from "lucide-react";
import { GameWindow } from "@/components/ui/GameWindow";
import { Prose } from "@/features/content/Prose";
import { formatDate } from "@/features/home/format";
import { safeHref } from "@/features/site/links";
import { loadSite } from "@/features/site/loadSite";

export const metadata: Metadata = { title: "Download", description: "Baixe o cliente do NexusPK, o launcher e as atualizações do jogo." };

async function Body() {
  await connection();
  const site = await loadSite();
  if (!site) return <p className="text-sm text-data-bad">Downloads indisponíveis no momento. Tente de novo em instantes.</p>;
  const { downloads } = site;
  const items = downloads.items.map((d) => ({ ...d, href: safeHref(d.url) })).filter((d) => d.href);
  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start">
      <GameWindow title="Arquivos do jogo">
        {downloads.intro ? (
          <div className="mb-4 space-y-3 text-sm leading-relaxed text-silver/90">
            <Prose text={downloads.intro} />
          </div>
        ) : null}
        {items.length === 0 ? (
          <p className="text-sm text-silver/70">Os arquivos do jogo serão publicados pela equipe em breve. Acompanhe as notícias.</p>
        ) : (
          <ul className="divide-y divide-steel/60">
            {items.map((d) => {
              const external = d.href!.startsWith("https://");
              return (
                <li key={d.href} className="flex flex-col gap-3 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="font-medium text-data-name">{d.name}</p>
                    {d.description ? <p className="text-sm text-silver/75">{d.description}</p> : null}
                    <p className="mt-0.5 text-xs text-silver/60">{[d.version, d.size].filter(Boolean).join(" · ")}</p>
                  </div>
                  <a
                    href={d.href!}
                    {...(external ? { target: "_blank", rel: "noopener noreferrer" } : {})}
                    className="btn-glow inline-flex shrink-0 items-center justify-center gap-2 rounded-sm border border-gold-bright/60 bg-gradient-to-b from-gold-bright to-gold px-4 py-2 text-sm font-semibold text-night"
                  >
                    <Download className="size-4" aria-hidden />
                    Baixar
                    {external ? <ExternalLink className="size-3.5" aria-hidden /> : null}
                  </a>
                </li>
              );
            })}
          </ul>
        )}
        {site.downloadsUpdatedUtc ? <p className="mt-4 text-xs text-silver/50">Atualizado em {formatDate(site.downloadsUpdatedUtc)}.</p> : null}
      </GameWindow>
      <div className="flex flex-col gap-4">
        <GameWindow title="Requisitos" as="aside">
          {downloads.requirements ? (
            <div className="space-y-2 text-sm text-silver/90">
              <Prose text={downloads.requirements} />
            </div>
          ) : (
            <p className="text-sm text-silver/70">Requisitos a publicar.</p>
          )}
        </GameWindow>
        <GameWindow title="Antes de jogar" as="aside">
          <ol className="list-decimal space-y-1.5 pl-5 text-sm text-silver/85">
            <li>
              <Link href="/criar-conta" className="text-arcane hover:underline">
                Crie sua conta
              </Link>
              .
            </li>
            <li>Baixe e instale o jogo.</li>
            <li>
              Confira o{" "}
              <Link href="/status" className="text-arcane hover:underline">
                status do servidor
              </Link>{" "}
              e entre.
            </li>
          </ol>
          <p className="mt-3 text-xs text-silver/60">
            Problema na instalação? Abra um chamado no{" "}
            <Link href="/suporte" className="text-arcane hover:underline">
              suporte
            </Link>
            .
          </p>
        </GameWindow>
      </div>
    </div>
  );
}

export default function DownloadPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Download</h1>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </div>
  );
}
