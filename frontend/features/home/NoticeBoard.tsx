"use client";

import Link from "next/link";
import { useState } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatDateTime } from "./format";
import type { HomeEventItem, HomeNewsItem } from "./types";

type Tab = "news" | "events";

const tagStyle = {
  news: "border-arcane/60 text-arcane",
  events: "border-amethyst/60 text-amethyst",
} as const;

export function NoticeBoard({ news, events }: { news: HomeNewsItem[]; events: HomeEventItem[] }) {
  const [tab, setTab] = useState<Tab>("news");
  const tabs: { id: Tab; label: string }[] = [
    { id: "news", label: "Notícias" },
    { id: "events", label: "Eventos" },
  ];

  return (
    <GameWindow
      title="Quadro de avisos"
      actions={
        <div role="tablist" aria-label="Filtrar avisos" className="flex gap-1">
          {tabs.map((t) => (
            <button
              key={t.id}
              role="tab"
              type="button"
              aria-selected={tab === t.id}
              onClick={() => setTab(t.id)}
              className={`px-2 py-0.5 text-xs ${tab === t.id ? "bg-bronze/40 text-gold-bright" : "text-silver/70 hover:text-gold"}`}
            >
              {t.label}
            </button>
          ))}
        </div>
      }
    >
      <ul role="tabpanel" className="divide-y divide-steel/60 text-sm">
        {tab === "news" &&
          (news.length === 0 ? (
            <li className="py-2 text-silver/70">Nenhuma notícia publicada.</li>
          ) : (
            news.map((n) => (
              <li key={n.slug} className="flex items-baseline gap-2 py-2">
                <span className={`shrink-0 border px-1.5 text-[11px] ${tagStyle.news}`}>Notícia</span>
                <Link href={`/noticias/${n.slug}`} className="truncate hover:text-gold">
                  {n.title}
                </Link>
                <span className="tabular ml-auto shrink-0 text-xs text-silver/60">{formatDateTime(n.publishedAtUtc)}</span>
              </li>
            ))
          ))}
        {tab === "events" &&
          (events.length === 0 ? (
            <li className="py-2 text-silver/70">Nenhum evento agendado.</li>
          ) : (
            events.map((e) => (
              <li key={e.slug} className="flex items-baseline gap-2 py-2">
                <span className={`shrink-0 border px-1.5 text-[11px] ${tagStyle.events}`}>{e.category}</span>
                <Link href={`/eventos/${e.slug}`} className="truncate hover:text-gold">
                  {e.name}
                </Link>
                <span className="tabular ml-auto shrink-0 text-xs text-silver/60">{formatDateTime(e.startsAtUtc)}</span>
              </li>
            ))
          ))}
      </ul>
    </GameWindow>
  );
}
