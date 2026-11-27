import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { PageHero } from "@/components/scene/PageHero";
import { ArtLink, kitArt } from "@/components/ui/ArtLink";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { guideGroups, guides } from "@/features/guides/content";

export const metadata: Metadata = {
  title: "O Jogo: guias",
  description: "Guias do NexusPK: primeiros passos, sistemas do servidor, aging, Mix List, horários de boss, missões, comandos, glossário e regras.",
};

/** Os três pilares da área "O Jogo": instalação, manual e guias. */
const pillars: { href: string; icon: KitIconName; title: string; text: string; cta: string }[] = [
  { href: "/instalacao", icon: "download", title: "Instalação", text: "Download, requisitos, passo a passo e solução dos erros mais comuns.", cta: "Instalar o jogo" },
  { href: "/manual", icon: "guia", title: "Manual do jogo", text: "Teclas, atributos, itens, aging, mapas, PvP, eventos e segurança em um só lugar.", cta: "Abrir o manual" },
  { href: "/guias/primeiros-passos", icon: "mapa", title: "Primeiros passos", text: "Da conta criada às primeiras horas: tribo, classe e as missões iniciais.", cta: "Começar por aqui" },
];

/** Páginas de consulta do dia a dia: horários, mix e a conta do aging. */
const tools: { href: string; icon: KitIconName; title: string; text: string }[] = [
  { href: "/bosses", icon: "boss", title: "Horários de boss", text: "Quando cada boss nasce, quanto falta e aviso no navegador antes do horário." },
  { href: "/mix", icon: "recompensas", title: "Mix List", text: "As fórmulas de mix de cada equipamento, com sheltoms e bônus." },
  { href: "/guias/aging#calculadora", icon: "trofeu", title: "Calculadora de Aging", text: "Quantos sheltoms faltam do aging de hoje até o que você quer." },
];

export default function GuidesPage() {
  return (
    <>
      <PageHero
        scene="vila-conversa"
        icon="guia"
        eyebrow="O Jogo"
        title="Guias do NexusPK"
        lead="Tudo o que você precisa para instalar, entender e evoluir no servidor, escrito pela equipe e mantido atualizado."
        aside={<ArtLink href="/instalacao" art={kitArt.botoes.downloadDoJogo} label="Download e instalação do jogo" sizes="300px" className="w-[280px] sm:w-[300px]" />}
      />
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-10 px-4 py-10">
        <ul className="grid gap-4 md:grid-cols-3">
          {pillars.map((p, i) => (
            <li key={p.href} data-reveal="up" style={{ "--reveal-delay": `${i * 80}ms` } as React.CSSProperties}>
              <Link href={p.href} className="guide-pillar group">
                <KitIcon name={p.icon} size={72} className="guide-pillar__icon" />
                <span className="font-display text-xl font-bold text-gold-bright">{p.title}</span>
                <span className="text-sm leading-relaxed text-silver/85">{p.text}</span>
                <span className="mt-auto inline-flex items-center gap-1.5 pt-2 text-sm font-semibold text-arcane group-hover:text-gold-bright">{p.cta} →</span>
              </Link>
            </li>
          ))}
        </ul>

        <section aria-labelledby="g-consulta">
          <div className="mb-3 flex items-center gap-3">
            <h2 id="g-consulta" className="font-display text-lg font-bold tracking-wide text-gold-bright uppercase">
              Consulta rápida
            </h2>
            <span aria-hidden className="gold-rule flex-1" />
          </div>
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {tools.map((t) => (
              <li key={t.href} data-reveal="up">
                <Link href={t.href} className="guide-card group">
                  <KitIcon name={t.icon} size={48} />
                  <span className="min-w-0">
                    <span className="block font-semibold text-silver group-hover:text-gold-bright">{t.title}</span>
                    <span className="mt-0.5 block text-sm leading-snug text-silver/70">{t.text}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>

        {guideGroups.map((group) => (
          <section key={group} aria-labelledby={`g-${group}`}>
            <div className="mb-3 flex items-center gap-3">
              <h2 id={`g-${group}`} className="font-display text-lg font-bold tracking-wide text-gold-bright uppercase">
                {group}
              </h2>
              <span aria-hidden className="gold-rule flex-1" />
            </div>
            <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {guides
                .filter((g) => g.group === group)
                .map((g) => (
                  <li key={g.slug} data-reveal="up">
                    <Link href={`/guias/${g.slug}`} className="guide-card group">
                      <KitIcon name={g.icon} size={48} />
                      <span className="min-w-0">
                        <span className="block font-semibold text-silver group-hover:text-gold-bright">{g.title}</span>
                        <span className="mt-0.5 block text-sm leading-snug text-silver/70">{g.summary}</span>
                      </span>
                    </Link>
                  </li>
                ))}
            </ul>
          </section>
        ))}

        <aside className="game-window flex flex-col items-center gap-4 px-5 py-5 text-center sm:flex-row sm:text-left" data-reveal="up">
          <Image src="/kit/prancha/item-pergaminho.webp" alt="" width={72} height={70} unoptimized className="shrink-0" />
          <div className="flex-1">
            <p className="font-display text-lg font-bold text-gold-bright">Não achou o que procurava?</p>
            <p className="text-sm text-silver/80">A equipe responde dúvidas pela central de suporte do portal. Dentro do jogo, ninguém da equipe pede a sua senha.</p>
          </div>
          <ArtLink href="/suporte" art={kitArt.botoes.suporte} label="Central de suporte" sizes="210px" className="w-[200px] shrink-0" />
        </aside>
      </div>
    </>
  );
}
