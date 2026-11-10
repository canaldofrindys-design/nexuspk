import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { Prose } from "@/features/content/Prose";
import { newsLabel, safeImage } from "@/features/content/labels";
import type { NewsArticle } from "@/features/content/types";
import { formatDate, formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

const slugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

async function load(slug: string) {
  if (!slugPattern.test(slug) || slug.length > 120) return null;
  const result = await apiSend<NewsArticle>("GET", `/api/v1/news/${slug}`);
  if (!result.ok && result.status === 404) return null;
  return result;
}

export async function generateMetadata({ params }: PageProps<"/noticias/[slug]">): Promise<Metadata> {
  const result = await load((await params).slug);
  return result?.ok ? { title: result.data.title, description: result.data.summary } : { title: "Notícia" };
}

async function Article({ params }: { params: Promise<{ slug: string }> }) {
  await connection();
  const result = await load((await params).slug);
  if (!result) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Notícia indisponível no momento. Tente de novo em instantes.</p>;
  const n = result.data;
  const tag = newsLabel(n.category);
  const image = safeImage(n.imageUrl);
  return (
    <article className="game-window">
      <header className="game-window__title px-4 py-3">
        <div className="mb-1 flex flex-wrap items-center gap-2 text-xs">
          <span className={`border px-1.5 ${tag.tone}`}>{tag.label}</span>
          <time dateTime={n.publishedUtc} className="tabular text-silver/70">
            {formatDate(n.publishedUtc)}
          </time>
          {n.authorName ? <span className="text-silver/60">por {n.authorName}</span> : null}
        </div>
        <h1 className="text-xl text-gold-bright">{n.title}</h1>
      </header>
      <div className="flex flex-col gap-4 p-4">
        {image ? (
          // eslint-disable-next-line @next/next/no-img-element -- imagem cadastrada pela equipe, tamanho livre
          <img src={image} alt="" className="h-auto w-full border border-steel/60" loading="lazy" decoding="async" width={1200} height={500} />
        ) : null}
        <p className="text-sm text-silver/80">{n.summary}</p>
        <Prose text={n.body} />
        {n.updatedUtc > n.publishedUtc ? <p className="text-xs text-silver/50">Atualizada em {formatDateTime(n.updatedUtc)}.</p> : null}
      </div>
    </article>
  );
}

export default function NewsArticlePage({ params }: PageProps<"/noticias/[slug]">) {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-10">
      <Link href="/noticias" className="mb-4 inline-block text-sm text-arcane hover:underline">
        Todas as notícias
      </Link>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Article params={params} />
      </Suspense>
    </div>
  );
}
