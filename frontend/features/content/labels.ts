import type { EventState } from "./types";

export const newsCategories = {
  noticia: { label: "Notícia", tone: "border-arcane/60 text-arcane" },
  atualizacao: { label: "Atualização", tone: "border-data-ok/60 text-data-ok" },
  manutencao: { label: "Manutenção", tone: "border-data-bad/60 text-data-bad" },
  evento: { label: "Evento", tone: "border-amethyst/60 text-amethyst" },
} as const;
export type NewsCategory = keyof typeof newsCategories;

export const eventCategories = {
  exp: "EXP",
  drop: "Drop",
  boss: "Boss",
  pvp: "PvP",
  invasao: "Invasão",
  especial: "Especial",
  comemorativo: "Comemorativo",
  gm: "Evento de GM",
} as const;
export type EventCategory = keyof typeof eventCategories;

export const eventStates: Record<EventState, { label: string; tone: string }> = {
  scheduled: { label: "Agendado", tone: "text-data-info" },
  live: { label: "Em andamento", tone: "text-data-ok" },
  ended: { label: "Encerrado", tone: "text-silver/60" },
  cancelled: { label: "Cancelado", tone: "text-data-bad" },
};

export function newsLabel(code: string) {
  return newsCategories[code as NewsCategory] ?? { label: code, tone: "border-steel text-silver" };
}

export function eventLabel(code: string) {
  return eventCategories[code as EventCategory] ?? code;
}

/** Só imagens do próprio site ou https. */
export function safeImage(url: string | null): string | null {
  if (!url) return null;
  return url.startsWith("/") && !url.startsWith("//") ? url : url.startsWith("https://") ? url : null;
}
