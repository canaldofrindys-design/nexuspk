const tz = "America/Sao_Paulo";

const dayFmt = new Intl.DateTimeFormat("en-CA", { timeZone: tz, year: "numeric", month: "2-digit", day: "2-digit" });

/** Data local (Brasília) "AAAA-MM-DD" de um instante. */
export function localDay(iso: string | Date) {
  return dayFmt.format(typeof iso === "string" ? new Date(iso) : iso);
}

export function formatLong(iso: string) {
  return new Intl.DateTimeFormat("pt-BR", {
    timeZone: tz,
    weekday: "long",
    day: "2-digit",
    month: "long",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(iso));
}

export function monthName(year: number, month: number) {
  const s = new Intl.DateTimeFormat("pt-BR", { timeZone: "UTC", month: "long", year: "numeric" }).format(new Date(Date.UTC(year, month - 1, 15)));
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export function currentYearMonth(now = new Date()) {
  const [y, m] = localDay(now).split("-").map(Number);
  return { year: y, month: m };
}

export function shiftMonth(year: number, month: number, delta: number) {
  const d = new Date(Date.UTC(year, month - 1 + delta, 1));
  return { year: d.getUTCFullYear(), month: d.getUTCMonth() + 1 };
}
