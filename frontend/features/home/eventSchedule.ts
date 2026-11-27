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

/** Todos os horários de início do evento no dia, em ordem ("01:00", "13:00"...). */
export function eventTimes(ev: Pick<ScheduledEvent, "time" | "times">): string[] {
  const list = ev.times && ev.times.length > 0 ? ev.times : [ev.time];
  return [...new Set(list)].filter((t) => /^\d{1,2}:\d{2}$/.test(t)).sort((a, b) => toMinutes(a) - toMinutes(b));
}

function toMinutes(t: string): number {
  const [h, m] = t.split(":").map(Number);
  return h * 60 + m;
}

/**
 * Próximas ocorrências de um evento fixo (a que está aberta agora entra primeiro), olhando todos os horários do dia,
 * até `max` e até duas semanas à frente.
 */
export function upcomingOccurrences(
  ev: Pick<ScheduledEvent, "days" | "time" | "times" | "durationMinutes">,
  now: number,
  max: number,
): { start: number; end: number }[] {
  const times = eventTimes(ev);
  if (times.length === 0 || ev.days.length === 0) return [];
  const duration = Math.max(1, ev.durationMinutes || 60) * 60_000;
  const brt = new Date(now - BRT_OFFSET_MS);
  const midnightUtc = Date.UTC(brt.getUTCFullYear(), brt.getUTCMonth(), brt.getUTCDate()) + BRT_OFFSET_MS;
  const out: { start: number; end: number }[] = [];
  // Começa um dia antes para pegar um evento que passou da meia-noite e ainda está aberto.
  for (let d = -1; d <= 14 && out.length < max; d++) {
    const weekday = (((brt.getUTCDay() + d) % 7) + 7) % 7;
    if (!ev.days.includes(weekday)) continue;
    for (const t of times) {
      const start = midnightUtc + d * DAY_MS + toMinutes(t) * 60_000;
      if (start + duration > now && out.length < max) out.push({ start, end: start + duration });
    }
  }
  return out;
}

/**
 * Próxima ocorrência (ou a que está acontecendo agora) de um evento fixo.
 * null quando o horário salvo é inválido ou nenhum dia foi marcado.
 */
export function nextOccurrence(ev: Pick<ScheduledEvent, "days" | "time" | "times" | "durationMinutes">, now: number): { start: number; end: number } | null {
  return upcomingOccurrences(ev, now, 1)[0] ?? null;
}

/** Próximo Bless Castle pelo horário do painel (sem ele, domingo 16:00). */
export function nextBlessCastle(schedule: ScheduledEvent[] | null | undefined, now: number = Date.now()): Date {
  const bless = schedule?.find((e) => e.kind === "bless") ?? defaultSchedule[0];
  const next = nextOccurrence(bless, now) ?? nextOccurrence(defaultSchedule[0], now)!;
  return new Date(next.start);
}

/** "Domingo" ou "Segunda e quinta" ou "Todo dia". */
export function describeWeekdays(daysIn: number[]): string {
  const days = [...new Set(daysIn)].sort((a, b) => a - b);
  if (days.length === 7) return "Todo dia";
  const names = days.map((d, i) => (i === 0 ? weekdayNames[d] : weekdayNames[d].toLowerCase()));
  return names.length > 1 ? `${names.slice(0, -1).join(", ")} e ${names[names.length - 1]}` : (names[0] ?? "");
}

/** "Domingo, 16:00" ou "Segunda e quinta, 20:00" ou "Todo dia, 01:00, 13:00 e 21:00". */
export function describeDays(ev: Pick<ScheduledEvent, "days" | "time" | "times">): string {
  const times = eventTimes(ev);
  const t = times.length > 1 ? `${times.slice(0, -1).join(", ")} e ${times[times.length - 1]}` : (times[0] ?? ev.time);
  return `${describeWeekdays(ev.days)}, ${t}`;
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
      href: kind === "boss" ? "/bosses" : kind === "bless" || kind === "sod" || kind === "hellsgate" ? "/manual#pvp" : "/eventos",
      hrefLabel: kind === "boss" ? "Horários e aviso" : kind === "bless" || kind === "sod" || kind === "hellsgate" ? "Como funciona" : "Ver agenda",
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
