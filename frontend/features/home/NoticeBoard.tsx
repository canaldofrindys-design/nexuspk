"use client";

import Link from "next/link";
import { useState } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { eventLabel, newsLabel } from "@/features/content/labels";
import { formatDateTime } from "./format";
import type { HomeEventItem, HomeNewsItem } from "./types";

type Tab = "all" | "news" | "events";

const eventTag = "border-amethyst/60 text-amethyst";

type Row = { key: string; href: string; tag: string; tone: string; title: string; at: string };

export function NoticeBoard({ news, events }: { news: HomeNewsItem[]; events: HomeEventItem[] }) {
  const [tab, setTab] = useState<Tab>("all");
  const tabs: { id: Tab; label: string }[] = [
    { id: "all", label: "Tudo" },
    { id: "news", label: "Notícias" },
    { id: "events", label: "Eventos" },
  ];

  const newsRows: Row[] = news.map((n) => ({
    key: `n-${n.slug}`,
    href: `/noticias/${n.slug}`,
    tag: newsLabel(n.category).label,
    tone: newsLabel(n.category).tone,
    title: n.title,
    at: n.publishedAtUtc,
  }));
  const eventRows: Row[] = events.map((e) => ({
    key: `e-${e.slug}`,
    href: `/eventos/${e.slug}`,
    tag: eventLabel(e.category),
    tone: eventTag,
    title: e.name,
    at: e.startsAtUtc,
  }));
  const rows =
    tab === "news" ? newsRows : tab === "events" ? eventRows : [...newsRows, ...eventRows].sort((a, b) => b.at.localeCompare(a.at)).slice(0, 6);
  const empty = { all: "Nenhum aviso publicado.", news: "Nenhuma notícia publicada.", events: "Nenhum evento agendado." }[tab];

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
              className={`border px-2 py-0.5 text-xs ${tab === t.id ? "border-bronze bg-medieval text-gold-bright" : "border-transparent text-silver/70 hover:text-gold"}`}
            >
              {t.label}
            </button>
          ))}
        </div>
      }
    >
      <ul role="tabpanel" className="divide-y divide-steel/60 text-sm">
        {rows.length === 0 ? (
          <li className="py-2 text-silver/70">{empty}</li>
        ) : (
          rows.map((r) => (
            <li key={r.key} className="flex items-baseline gap-2 py-2">
              <span className={`w-24 shrink-0 border px-1.5 text-center text-[11px] ${r.tone}`}>{r.tag}</span>
              <Link href={r.href} className="min-w-0 hover:text-gold sm:truncate">
                {r.title}
              </Link>
              <span className="tabular ml-auto shrink-0 text-xs text-silver/60">{formatDateTime(r.at)}</span>
            </li>
          ))
        )}
      </ul>
      <p className="mt-2 flex justify-end gap-4 text-xs">
        <Link href="/noticias" className="text-arcane hover:underline">
          Todas as notícias
        </Link>
        <Link href="/eventos" className="text-arcane hover:underline">
          Todos os eventos
        </Link>
      </p>
    </GameWindow>
  );
}
