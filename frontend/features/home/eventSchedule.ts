import type { KitIconName } from "@/components/ui/KitIcon";
import type { SceneKey } from "@/components/scene/scenes";
import type { ScheduledEvent, ScheduledEventKind } from "@/features/site/types";
import type { HomeEventItem } from "./types";

/**
 * Horários dos eventos do servidor para o banner da home: próxima ocorrência de cada evento fixo da semana
 * (Bless Castle, SoD...) e os eventos da agenda do painel. Horários em Brasília (UTC−3 o ano todo).
 */

/** Sem nada salvo no painel (ou API fora): o Bless Castle de sempre. Igual ao padrão da API. */
export const defaultSchedule: ScheduledEvent[] = [
  { name: "Bless Castle", kind: "bless", days: [0], time: "16:00", durationMinutes: 60, description: "Guerra de clãs pela posse do castelo" },
];

export const scheduleKinds: Record<ScheduledEventKind, { label: string; icon: KitIconName; scene: SceneKey; mood: "sun" | "moon" | "day"; text: string }> = {
  bless: {
    label: "Bless Castle",
    icon: "guildasBandeira",
    scene: "luar",
    mood: "moon",
    text: "Os clãs disputam o castelo. Quem segura a coroa no fim leva o castelo e o topo da semana.",
  },
  sod: {
    label: "Survival of the Death",
    icon: "trofeu",
    scene: "torre-vigia",
    mood: "day",
    text: "Os três melhores clãs ganham as coroas de ouro, prata e bronze, com bônus de HP.",
  },
  hellsgate: { label: "Arena Hellsgate", icon: "mapa", scene: "luar", mood: "moon", text: "Arena de desafio do servidor. Reúna o grupo antes da abertura." },
  boss: { label: "Boss mundial", icon: "boss", scene: "torre-vigia", mood: "day", text: "Boss com horário marcado e prêmios para quem estiver lá." },
  pvp: { label: "PvP", icon: "pvp", scene: "luar", mood: "moon", text: "Combate entre jogadores valendo pontos no ranking PvP." },
  evento: { label: "Evento", icon: "eventos", scene: "mercado-entardecer", mood: "sun", text: "Evento da equipe no servidor. Fique de olho no horário." },
};

const BRT_OFFSET_MS = 3 * 3600_000;
const DAY_MS = 86400_000;

export const weekdayNames = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"] as const;

/**
 * Próxima ocorrência (ou a que está acontecendo agora) de um evento fixo.
 * null quando o horário salvo é inválido ou nenhum dia foi marcado.
 */
export function nextOccurrence(ev: Pick<ScheduledEvent, "days" | "time" | "durationMinutes">, now: number): { start: number; end: number } | null {
  const m = /^(\d{1,2}):(\d{2})$/.exec(ev.time);
  if (!m || ev.days.length === 0) return null;
  const minutes = Number(m[1]) * 60 + Number(m[2]);
  const duration = Math.max(1, ev.durationMinutes || 60) * 60_000;
  const brt = new Date(now - BRT_OFFSET_MS);
  const midnightUtc = Date.UTC(brt.getUTCFullYear(), brt.getUTCMonth(), brt.getUTCDate()) + BRT_OFFSET_MS;
  // Começa um dia antes para pegar um evento que passou da meia-noite e ainda está aberto.
  for (let d = -1; d <= 7; d++) {
    const weekday = (((brt.getUTCDay() + d) % 7) + 7) % 7;
    if (!ev.days.includes(weekday)) continue;
    const start = midnightUtc + d * DAY_MS + minutes * 60_000;
    if (start + duration > now) return { start, end: start + duration };
  }
  return null;
}

/** Próximo Bless Castle pelo horário do painel (sem ele, domingo 16:00). */
export function nextBlessCastle(schedule: ScheduledEvent[] | null | undefined, now: number = Date.now()): Date {
  const bless = schedule?.find((e) => e.kind === "bless") ?? defaultSchedule[0];
  const next = nextOccurrence(bless, now) ?? nextOccurrence(defaultSchedule[0], now)!;
  return new Date(next.start);
}

/** "Domingo, 16:00" ou "Segunda e quinta, 20:00" ou "Todo dia, 21:00". */
export function describeDays(ev: Pick<ScheduledEvent, "days" | "time">): string {
  const days = [...new Set(ev.days)].sort((a, b) => a - b);
  if (days.length === 7) return `Todo dia, ${ev.time}`;
  const names = days.map((d, i) => (i === 0 ? weekdayNames[d] : weekdayNames[d].toLowerCase()));
  const list = names.length > 1 ? `${names.slice(0, -1).join(", ")} e ${names[names.length - 1]}` : (names[0] ?? "");
  return `${list}, ${ev.time}`;
}

/** Evento que vai para o banner: fixo da semana ou da agenda do painel. */
export type FeaturedEvent = {
  key: string;
  name: string;
  kind: ScheduledEventKind;
  description: string;
  /** "Domingo, 16:00" (fixo) ou a data da agenda. */
  when: string;
  start: number;
  end: number | null;
  href: string;
  hrefLabel: string;
};

const agendaKind: Record<string, ScheduledEventKind> = { boss: "boss", pvp: "pvp" };

/** Até quantos dias à frente um evento da agenda entra no banner. */
const AGENDA_DAYS = 7;

/**
 * Eventos do banner, os que estão acontecendo primeiro e depois pelo horário de início.
 * Agenda: só os que estão abertos ou começam nos próximos sete dias.
 */
export function featuredEvents(schedule: ScheduledEvent[], agenda: HomeEventItem[], now: number, max = 3): FeaturedEvent[] {
  const list: FeaturedEvent[] = [];
  schedule.forEach((ev, i) => {
    const next = nextOccurrence(ev, now);
    if (!next) return;
    const kind = scheduleKinds[ev.kind] ? ev.kind : "evento";
    list.push({
      key: `fixo-${i}-${ev.name}`,
      name: ev.name,
      kind,
      description: ev.description || scheduleKinds[kind].text,
      when: describeDays(ev),
      start: next.start,
      end: next.end,
      href: kind === "bless" || kind === "sod" || kind === "hellsgate" ? "/manual#pvp" : "/eventos",
      hrefLabel: kind === "bless" || kind === "sod" || kind === "hellsgate" ? "Como funciona" : "Ver agenda",
    });
  });
  const seen = new Set<string>();
  for (const ev of agenda) {
    if (seen.has(ev.slug)) continue;
    seen.add(ev.slug);
    const start = Date.parse(ev.startsAtUtc);
    const end = ev.endsAtUtc ? Date.parse(ev.endsAtUtc) : null;
    if (Number.isNaN(start) || (end !== null && end <= now) || start > now + AGENDA_DAYS * DAY_MS) continue;
    if (end === null && start < now - 3600_000) continue;
    const kind = agendaKind[ev.category] ?? "evento";
    list.push({
      key: `agenda-${ev.slug}`,
      name: ev.name,
      kind,
      description: scheduleKinds[kind].text,
      when: "",
      start,
      end,
      href: `/eventos/${ev.slug}`,
      hrefLabel: "Detalhes do evento",
    });
  }
  const live = (e: FeaturedEvent) => e.start <= now;
  return list.sort((a, b) => Number(live(b)) - Number(live(a)) || a.start - b.start).slice(0, max);
}
