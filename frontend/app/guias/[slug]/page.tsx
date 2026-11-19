import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PageHero } from "@/components/scene/PageHero";
import { KitIcon } from "@/components/ui/KitIcon";
import { Chapter, Toc } from "@/features/guides/Blocks";
import { guideBySlug, guides } from "@/features/guides/content";

export function generateStaticParams() {
  return guides.map((g) => ({ slug: g.slug }));
}

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const g = guideBySlug((await params).slug);
  return g ? { title: g.title, description: g.summary } : {};
}

export default async function GuidePage({ params }: { params: Promise<{ slug: string }> }) {
  const guide = guideBySlug((await params).slug);
  if (!guide) notFound();
  const others = guides.filter((g) => g.slug !== guide.slug);
  return (
    <>
      <PageHero
        scene={guide.scene}
        icon={guide.icon}
        eyebrow={`Guias · ${guide.group}`}
        title={guide.title}
        lead={guide.summary}
      />
      <div className="mx-auto w-full max-w-7xl px-4 py-8">
        <nav aria-label="Você está em" className="mb-5 text-sm text-silver/70">
          <Link href="/guias" className="hover:text-gold-bright">
            O Jogo
          </Link>
          <span aria-hidden className="mx-2 text-bronze">›</span>
          <span className="text-silver">{guide.title}</span>
        </nav>
        <div className="grid grid-cols-[minmax(0,1fr)] gap-6 lg:grid-cols-[220px_minmax(0,1fr)] lg:items-start">
          <div className="min-w-0 lg:sticky lg:top-24">
            <Toc sections={guide.sections} />
          </div>
          <div className="flex min-w-0 flex-col gap-5">
            {guide.sections.map((s, i) => (
              <Chapter key={s.id} section={s} index={i + 1} />
            ))}
            <nav aria-label="Outros guias" className="mt-2">
              <p className="mb-2 font-display text-sm tracking-[0.18em] text-gold-bright uppercase">Outros guias</p>
              <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                {others.map((g) => (
                  <li key={g.slug}>
                    <Link href={`/guias/${g.slug}`} className="guide-card guide-card--compact group">
                      <KitIcon name={g.icon} size={32} />
                      <span className="font-semibold text-silver group-hover:text-gold-bright">{g.title}</span>
                    </Link>
                  </li>
                ))}
                <li>
                  <Link href="/manual" className="guide-card guide-card--compact group">
                    <KitIcon name="guia" size={32} />
                    <span className="font-semibold text-silver group-hover:text-gold-bright">Manual do jogo</span>
                  </Link>
                </li>
              </ul>
            </nav>
          </div>
        </div>
      </div>
    </>
  );
}
