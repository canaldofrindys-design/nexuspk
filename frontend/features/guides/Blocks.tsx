import Link from "next/link";
import { ArrowRight, Info, TriangleAlert } from "lucide-react";
import { KitIcon } from "@/components/ui/KitIcon";
import type { Block, Section } from "./types";

/** Desenha os blocos de um guia. Tudo é texto do repositório renderizado como texto pelo React. */
export function Blocks({ blocks }: { blocks: Block[] }) {
  return (
    <div className="guide-prose flex flex-col gap-4">
      {blocks.map((b, i) => {
        switch (b.kind) {
          case "p":
            return <p key={i}>{b.text}</p>;
          case "list":
            return (
              <ul key={i} className="guide-list">
                {b.items.map((t) => (
                  <li key={t}>{t}</li>
                ))}
              </ul>
            );
          case "steps":
            return (
              <ol key={i} className="guide-steps">
                {b.items.map((s, n) => (
                  <li key={s.title}>
                    <span aria-hidden className="guide-steps__n tabular">
                      {n + 1}
                    </span>
                    <div>
                      <p className="font-semibold text-gold-bright">{s.title}</p>
                      <p className="mt-0.5">{s.text}</p>
                    </div>
                  </li>
                ))}
              </ol>
            );
          case "table":
            return (
              <div key={i} className="guide-table-wrap">
                <table className="guide-table">
                  {b.caption ? <caption className="sr-only">{b.caption}</caption> : null}
                  <thead>
                    <tr>
                      {b.head.map((h, j) => (
                        <th key={j} scope="col">
                          {h}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {b.rows.map((r, j) => (
                      <tr key={j}>
                        {r.map((c, k) =>
                          k === 0 ? (
                            <th key={k} scope="row">
                              {c}
                            </th>
                          ) : (
                            <td key={k}>{c}</td>
                          ),
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            );
          case "facts":
            return (
              <dl key={i} className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                {b.items.map((f) => (
                  <div key={f.label} className="guide-fact">
                    <dt className="text-[11px] tracking-wide text-silver/70 uppercase">{f.label}</dt>
                    <dd className="mt-0.5 font-display text-lg font-bold text-gold-bright">{f.value}</dd>
                  </div>
                ))}
              </dl>
            );
          case "note": {
            const Icon = b.tone === "alerta" ? TriangleAlert : Info;
            return (
              <p key={i} className={`guide-note guide-note--${b.tone}`}>
                <Icon aria-hidden className="mt-0.5 size-4 shrink-0" />
                <span>{b.text}</span>
              </p>
            );
          }
          case "links":
            return (
              <ul key={i} className="flex flex-wrap gap-2">
                {b.items.map((l) => (
                  <li key={l.href}>
                    <Link href={l.href} className="guide-chip">
                      {l.label}
                      <ArrowRight aria-hidden className="size-3.5" />
                    </Link>
                  </li>
                ))}
              </ul>
            );
        }
      })}
    </div>
  );
}

/** Capítulo com título em plaquinha (ícone do kit + versalete) e âncora para o índice. */
export function Chapter({ section, index }: { section: Section; index?: number }) {
  return (
    <section id={section.id} aria-labelledby={`${section.id}-t`} className="game-window scroll-mt-24" data-reveal="up">
      <header className="game-window__title flex items-center gap-2 px-4 py-2.5">
        {section.icon ? <KitIcon name={section.icon} size={30} className="-my-1.5" /> : null}
        <h2 id={`${section.id}-t`} className="text-sm text-gold-bright sm:text-base">
          {index != null ? <span className="mr-2 text-silver/60 tabular">{String(index).padStart(2, "0")}</span> : null}
          {section.title}
        </h2>
      </header>
      <div className="px-4 py-4 sm:px-5">
        <Blocks blocks={section.blocks} />
      </div>
    </section>
  );
}

/** Índice lateral (sumário) de uma página de guia. No celular vira uma faixa de atalhos. */
export function Toc({ sections, title = "Nesta página" }: { sections: Pick<Section, "id" | "title">[]; title?: string }) {
  return (
    <nav aria-label={title} className="guide-toc">
      <p className="font-display text-xs tracking-[0.2em] text-gold-bright uppercase">{title}</p>
      <ol className="mt-2 flex gap-1.5 overflow-x-auto pb-1 [scrollbar-width:none] lg:flex-col lg:gap-0 lg:overflow-visible lg:pb-0">
        {sections.map((s, i) => (
          <li key={s.id} className="shrink-0">
            <a href={`#${s.id}`} className="guide-toc__link">
              <span className="tabular text-silver/50">{String(i + 1).padStart(2, "0")}</span>
              {s.title}
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}
