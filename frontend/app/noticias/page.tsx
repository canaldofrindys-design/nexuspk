import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { newsCategories, newsLabel, type NewsCategory } from "@/features/content/labels";
import type { NewsPage } from "@/features/content/types";
import { formatDate } from "@/features/home/format";
import { Pager } from "@/features/ranking/Pager";
import { Tabs } from "@/features/ranking/Tabs";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Notícias", description: "Notícias, atualizações e manutenções do NexusPK." };

type Query = { categoria?: string; pagina?: string };

function parse(q: Query) {
  const category = q.categoria && q.categoria in newsCategories ? (q.categoria as NewsCategory) : undefined;
  const page = Math.max(1, Number.parseInt(q.pagina ?? "1", 10) || 1);
  return { category, page };
}

function url(category: NewsCategory | undefined, page = 1) {
  const p = new URLSearchParams();
  if (category) p.set("categoria", category);
  if (page > 1) p.set("pagina", String(page));
  const qs = p.toString();
  return qs ? `/noticias?${qs}` : "/noticias";
}

async function NewsBody({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const s = parse(await searchParams);
  const qs = new URLSearchParams({ pagina: String(s.page), porPagina: "10" });
  if (s.category) qs.set("categoria", s.category);
  const result = await apiSend<NewsPage>("GET", `/api/v1/news?${qs}`);

  return (
    <>
      <div className="mb-4">
        <Tabs
          label="Categoria"
          tabs={[
            { href: url(undefined), label: "Tudo", active: !s.category },
            ...(Object.keys(newsCategories) as NewsCategory[]).map((c) => ({ href: url(c), label: newsCategories[c].label, active: s.category === c })),
          ]}
        />
      </div>
      <GameWindow title={s.category ? newsCategories[s.category].label : "Quadro de avisos"}>
        {!result.ok ? (
          <p className="text-sm text-data-bad">Notícias indisponíveis no momento. Tente de novo em instantes.</p>
        ) : result.data.items.length === 0 ? (
          <p className="text-sm text-silver/70">Nenhuma notícia publicada{s.category ? " nesta categoria" : ""}.</p>
        ) : (
          <>
            <ul className="divide-y divide-steel/60">
              {result.data.items.map((n) => {
                const tag = newsLabel(n.category);
                return (
                  <li key={n.slug} className="py-3">
                    <div className="flex flex-wrap items-baseline gap-2">
                      <span className={`shrink-0 border px-1.5 text-[11px] ${tag.tone}`}>{tag.label}</span>
                      {n.pinned ? <span className="text-[11px] text-gold">Fixada</span> : null}
                      <Link href={`/noticias/${n.slug}`} className="font-medium text-silver hover:text-gold-bright hover:underline">
                        {n.title}
                      </Link>
                      <span className="tabular ml-auto text-xs text-silver/60">{formatDate(n.publishedUtc)}</span>
                    </div>
                    <p className="mt-1 text-sm text-silver/75">{n.summary}</p>
                  </li>
                );
              })}
            </ul>
            <Pager page={result.data.page} pageSize={result.data.pageSize} total={result.data.total} href={(p) => url(s.category, p)} />
          </>
        )}
      </GameWindow>
    </>
  );
}

export default function NewsListPage({ searchParams }: PageProps<"/noticias">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Notícias</h1>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <NewsBody searchParams={searchParams as Promise<Query>} />
      </Suspense>
    </div>
  );
}
