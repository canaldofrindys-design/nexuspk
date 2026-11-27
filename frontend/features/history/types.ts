/** Históricos públicos: níveis (fotos diárias do portal), aging e craft (log de itens do jogo). */
export type HistoryPage<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  available: boolean;
  configured: boolean;
  since: string | null;
};

export type LevelUpEvent = {
  name: string;
  classCode: number;
  className: string;
  fromLevel: number;
  toLevel: number;
  /** "AAAA-MM-DD": dia em que subiu; sinceDay quando faltam fotos entre as duas. */
  day: string;
  sinceDay: string | null;
  today: boolean;
};

export type ItemHistoryEvent = { character: string; item: string; itemId: string | null; atUtc: string };
