import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { Download, ExternalLink } from "lucide-react";
import { PageHero } from "@/components/scene/PageHero";
import { ArtLink, kitArt } from "@/components/ui/ArtLink";
import { GameWindow } from "@/components/ui/GameWindow";
import { KitIcon } from "@/components/ui/KitIcon";
import { Blocks, Chapter, Toc } from "@/features/guides/Blocks";
import { fallbackDownloads, installSteps, requirements, runtimes } from "@/features/guides/install";
import { safeHref } from "@/features/site/links";
import { loadSite } from "@/features/site/loadSite";

export const metadata: Metadata = {
  title: "Instalação",
  description: "Como instalar o NexusPK: download do jogo completo e do update, requisitos do computador, passo a passo e solução de problemas.",
};

type Item = { name: string; description: string | null; meta: string; href: string };

/** Arquivos do jogo: os cadastrados no painel; sem nenhum, os links oficiais publicados no site antigo. */
async function Files() {
  await connection();
  const site = await loadSite();
  const fromPanel: Item[] = (site?.downloads.items ?? [])
    .map((d) => ({ name: d.name, description: d.description, meta: [d.version, d.size].filter(Boolean).join(" · "), href: safeHref(d.url) ?? "" }))
    .filter((d) => d.href);
  const items: Item[] = fromPanel.length
    ? fromPanel
    : fallbackDownloads.map((d) => ({ name: d.name, description: d.description, meta: [d.size, d.host].filter(Boolean).join(" · "), href: d.href }));
  return (
    <ul className="flex flex-col gap-2">
      {items.map((d, i) => {
        const external = d.href.startsWith("https://");
        return (
          <li key={d.href} className="install-file">
            <span aria-hidden className="install-file__n tabular">{i + 1}</span>
            <div className="min-w-0 flex-1 basis-[60%]">
              <p className="font-semibold text-data-name">{d.name}</p>
              {d.description ? <p className="text-sm text-silver/75">{d.description}</p> : null}
              {d.meta ? <p className="mt-0.5 text-xs text-silver/60">{d.meta}</p> : null}
            </div>
            <a
              href={d.href}
              {...(external ? { target: "_blank", rel: "noopener noreferrer" } : {})}
              className="nx-btn btn-glow inline-flex shrink-0 items-center justify-center gap-2 px-4 py-2 text-sm font-semibold max-sm:w-full"
              aria-label={`Baixar ${d.name}${external ? " (abre em outra aba)" : ""}`}
            >
              <Download aria-hidden className="size-4" />
              Baixar
              {external ? <ExternalLink aria-hidden className="size-3.5" /> : null}
            </a>
          </li>
        );
      })}
    </ul>
  );
}

const toc = [
  { id: "arquivos", title: "Arquivos do jogo" },
  { id: "requisitos", title: "Requisitos" },
  ...installSteps.map((s) => ({ id: s.id, title: s.title })),
  { id: "componentes", title: "Componentes e drivers" },
];

export default function InstallPage() {
  return (
    <>
      <PageHero
        scene="jornada"
        icon="download"
        eyebrow="O Jogo"
        title="Instalação"
        lead="Do download ao primeiro login em seis passos, com os requisitos do computador e a solução dos erros mais comuns."
        aside={<ArtLink href="/criar-conta" art={kitArt.botoes.criarConta} label="Criar conta" sizes="260px" className="w-[240px] sm:w-[260px]" />}
      />
      <div className="mx-auto w-full max-w-7xl px-4 py-8">
        <nav aria-label="Você está em" className="mb-5 text-sm text-silver/70">
          <Link href="/guias" className="hover:text-gold-bright">
            O Jogo
          </Link>
          <span aria-hidden className="mx-2 text-bronze">›</span>
          <span className="text-silver">Instalação</span>
        </nav>
        <div className="grid grid-cols-[minmax(0,1fr)] gap-6 lg:grid-cols-[220px_minmax(0,1fr)] lg:items-start">
          <div className="min-w-0 lg:sticky lg:top-24">
            <Toc sections={toc} />
          </div>
          <div className="flex min-w-0 flex-col gap-5">
            <div className="grid gap-5 xl:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)] xl:items-start">
              <section id="arquivos" aria-labelledby="arquivos-t" className="game-window scroll-mt-24" data-reveal="up">
                <header className="game-window__title flex items-center gap-2 px-4 py-2.5">
                  <KitIcon name="download" size={30} className="-my-1.5" />
                  <h2 id="arquivos-t" className="text-sm text-gold-bright sm:text-base">
                    Arquivos do jogo
                  </h2>
                </header>
                <div className="px-4 py-4">
                  <Suspense fallback={<p className="text-sm text-silver/60">Carregando os links…</p>}>
                    <Files />
                  </Suspense>
                  <p className="mt-3 text-xs text-silver/60">
                    Baixe os dois: o jogo completo e o update obrigatório. Versões e notas de atualização ficam na página{" "}
                    <Link href="/download" className="text-arcane hover:underline">
                      Download
                    </Link>
                    .
                  </p>
                </div>
              </section>
              <section id="requisitos" aria-labelledby="requisitos-t" className="game-window scroll-mt-24" data-reveal="up">
                <header className="game-window__title flex items-center gap-2 px-4 py-2.5">
                  <KitIcon name="personagem" size={30} className="-my-1.5" />
                  <h2 id="requisitos-t" className="text-sm text-gold-bright sm:text-base">
                    Requisitos
                  </h2>
                </header>
                <div className="px-4 py-4">
                  <Blocks blocks={[{ kind: "table", caption: "Requisitos do computador", head: requirements.head, rows: requirements.rows }]} />
                  <p className="mt-3 text-xs text-silver/60">Valores de referência para jogar com fluidez em 1920×1080. Em PCs mais modestos, desligue as sombras (F11).</p>
                </div>
              </section>
            </div>
            {installSteps.map((s, i) => (
              <Chapter key={s.id} section={s} index={i + 1} />
            ))}
            <GameWindow title="Componentes e drivers" icon="recompensas" className="scroll-mt-24">
              <div id="componentes" className="-mt-3 scroll-mt-24" />
              <ul className="grid gap-2 sm:grid-cols-2">
                {runtimes.map((r) => (
                  <li key={r.href}>
                    <a href={r.href} target="_blank" rel="noopener noreferrer" className="guide-card guide-card--compact group h-full">
                      <ExternalLink aria-hidden className="size-4 shrink-0 text-arcane" />
                      <span>
                        <span className="block font-semibold text-silver group-hover:text-gold-bright">{r.label}</span>
                        <span className="block text-xs text-silver/70">{r.text}</span>
                      </span>
                    </a>
                  </li>
                ))}
              </ul>
              <p className="mt-3 text-xs text-silver/60">Links oficiais da Microsoft, NVIDIA e AMD (abrem em outra aba).</p>
            </GameWindow>
          </div>
        </div>
      </div>
    </>
  );
}
