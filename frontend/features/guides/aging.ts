/**
 * Tabela de Aging do NexusPK (fonte: wiki do site antigo do servidor, página aging). Usada pelo guia
 * de Aging e pela calculadora de sheltoms. Quando a equipe mudar o aging no servidor, muda aqui.
 */

/** Ordem dos sheltoms, do mais comum ao mais raro. */
export const sheltomOrder = ["Fadeo", "Sparky", "Raident", "Transparo", "Murky", "Devine", "Celesto", "Mirage", "Inferna", "Enigma", "Bellum", "Oredo"] as const;

export type Sheltom = (typeof sheltomOrder)[number];

export type AgingLevel = { level: number; sheltoms: Partial<Record<Sheltom, number>>; risk: string };

/** Primeiro nível em que o aging pode falhar. */
export const agingFailFrom = 24;
export const agingMax = 26;

export const agingLevels: AgingLevel[] = [
  { level: 1, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 1 }, risk: "Sem falha" },
  { level: 2, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2 }, risk: "Sem falha" },
  { level: 3, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 1 }, risk: "Sem falha" },
  { level: 4, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 2 }, risk: "Sem falha" },
  { level: 5, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 2, Murky: 1 }, risk: "Sem falha" },
  { level: 6, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 2, Murky: 2 }, risk: "Sem falha" },
  { level: 7, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 2, Murky: 2, Devine: 1 }, risk: "Sem falha" },
  { level: 8, sheltoms: { Fadeo: 2, Sparky: 2, Raident: 2, Transparo: 2, Murky: 2, Devine: 2 }, risk: "Sem falha" },
  { level: 9, sheltoms: { Sparky: 2, Raident: 2, Transparo: 2, Murky: 2, Devine: 2, Celesto: 1 }, risk: "Sem falha" },
  { level: 10, sheltoms: { Sparky: 2, Raident: 2, Transparo: 2, Murky: 2, Devine: 2, Celesto: 2 }, risk: "Sem falha" },
  { level: 11, sheltoms: { Raident: 2, Transparo: 2, Murky: 2, Devine: 2, Celesto: 2, Mirage: 1 }, risk: "Sem falha" },
  { level: 12, sheltoms: { Raident: 2, Transparo: 2, Murky: 2, Devine: 2, Celesto: 2, Mirage: 2 }, risk: "Sem falha" },
  { level: 13, sheltoms: { Transparo: 2, Murky: 2, Devine: 2, Celesto: 2, Mirage: 2, Inferna: 1 }, risk: "Sem falha" },
  { level: 14, sheltoms: { Transparo: 2, Murky: 2, Devine: 2, Celesto: 2, Mirage: 2, Inferna: 2 }, risk: "Sem falha" },
  { level: 15, sheltoms: { Murky: 2, Devine: 2, Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 1 }, risk: "Sem falha" },
  { level: 16, sheltoms: { Murky: 2, Devine: 2, Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 2 }, risk: "Sem falha" },
  { level: 17, sheltoms: { Devine: 2, Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 1 }, risk: "Sem falha" },
  { level: 18, sheltoms: { Devine: 2, Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 2 }, risk: "Sem falha" },
  { level: 19, sheltoms: { Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 1 }, risk: "Sem falha" },
  { level: 20, sheltoms: { Celesto: 2, Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Sem falha" },
  { level: 21, sheltoms: { Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Sem falha" },
  { level: 22, sheltoms: { Mirage: 2, Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Sem falha" },
  { level: 23, sheltoms: { Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Sem falha" },
  { level: 24, sheltoms: { Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Pode falhar" },
  { level: 25, sheltoms: { Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Pode falhar" },
  { level: 26, sheltoms: { Inferna: 2, Enigma: 2, Bellum: 2, Oredo: 2 }, risk: "Pode falhar (máximo)" },
];

/** "Fadeo ×2, Sparky ×2" na ordem dos sheltoms. */
export function describeSheltoms(list: Partial<Record<Sheltom, number>>): string {
  return sheltomOrder
    .filter((n) => (list[n] ?? 0) > 0)
    .map((n) => `${n} ×${list[n]}`)
    .join(", ");
}

/** Sheltoms somados para subir do nível `from` (o que o item tem hoje) até `to`. */
export function sheltomsBetween(from: number, to: number): Partial<Record<Sheltom, number>> {
  const total: Partial<Record<Sheltom, number>> = {};
  for (const l of agingLevels) {
    if (l.level <= from || l.level > to) continue;
    for (const [n, q] of Object.entries(l.sheltoms) as [Sheltom, number][]) total[n] = (total[n] ?? 0) + q;
  }
  return total;
}
