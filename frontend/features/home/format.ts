const tz = "America/Sao_Paulo";

export function formatDateTime(iso: string) {
  return new Intl.DateTimeFormat("pt-BR", { timeZone: tz, day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }).format(new Date(iso));
}

export function formatTime(iso: string) {
  return new Intl.DateTimeFormat("pt-BR", { timeZone: tz, hour: "2-digit", minute: "2-digit" }).format(new Date(iso));
}

export function formatDate(iso: string) {
  return new Intl.DateTimeFormat("pt-BR", { timeZone: tz, day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(iso));
}

export function formatNumber(value: number) {
  return new Intl.NumberFormat("pt-BR").format(value);
}
