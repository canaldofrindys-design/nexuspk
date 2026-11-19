import type { Metadata } from "next";
import Link from "next/link";
import { PageHero } from "@/components/scene/PageHero";
import { ArtLink, kitArt } from "@/components/ui/ArtLink";
import { Chapter, Toc } from "@/features/guides/Blocks";
import { manual } from "@/features/guides/manual";

export const metadata: Metadata = {
  title: "Manual do jogo",
  description: "Manual do NexusPK: teclas, atributos, evolução, itens, aging e mix, mapas, PvP, eventos e segurança da conta.",
};

export default function ManualPage() {
  return (
    <>
      <PageHero
        scene="torre-vigia"
        icon="guia"
        eyebrow="O Jogo"
        title="Manual do jogo"
        lead="A referência do NexusPK em nove capítulos. Para o detalhe de cada sistema, siga os links para os guias."
        aside={<ArtLink href="/guias" art={kitArt.botoes.guia} label="Todos os guias do jogo" sizes="270px" className="w-[250px] sm:w-[270px]" />}
      />
      <div className="mx-auto w-full max-w-7xl px-4 py-8">
        <nav aria-label="Você está em" className="mb-5 text-sm text-silver/70">
          <Link href="/guias" className="hover:text-gold-bright">
            O Jogo
          </Link>
          <span aria-hidden className="mx-2 text-bronze">›</span>
          <span className="text-silver">Manual do jogo</span>
        </nav>
        <div className="grid grid-cols-[minmax(0,1fr)] gap-6 lg:grid-cols-[220px_minmax(0,1fr)] lg:items-start">
          <div className="min-w-0 lg:sticky lg:top-24">
            <Toc sections={manual} title="Capítulos" />
          </div>
          <div className="flex min-w-0 flex-col gap-5">
            {manual.map((s, i) => (
              <Chapter key={s.id} section={s} index={i + 1} />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
