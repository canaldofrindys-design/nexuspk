/** Datas "AAAA-MM-DD" (DateOnly da API) para "DD/MM". */
export function dayMonth(isoDate: string) {
  const [, m, d] = isoDate.split("-");
  return `${d}/${m}`;
}

export const periodLabel = { geral: "Geral", diario: "Hoje", semanal: "Semana", mensal: "Mês" } as const;
export type Period = keyof typeof periodLabel;
