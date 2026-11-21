export type RankingPage<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  available: boolean;
  periodStart: string | null;
  countingSince: string | null;
  generatedUtc: string;
};

export type LevelEntry = {
  position: number;
  name: string;
  classCode: number;
  className: string;
  level: number;
  levelsGained: number | null;
  clan: string | null;
};

export type PvpEntry = {
  position: number;
  name: string;
  classCode: number;
  className: string;
  battlePoints: number;
  weeklyBattlePoints: number;
  kills: number;
  deaths: number;
  isPvpKing: boolean;
  /** Pontos PvP (decidem a patente) e o nível da patente: 0 nenhuma, 1 a 7, 8 Rei PvP. */
  pvpPoints?: number;
  patent?: number;
};

export type ClanEntry = {
  position: number;
  name: string;
  members: number;
  points: number;
  wins: number;
  losses: number;
  createdUtc: string | null;
};

export type CharacterProfile = {
  name: string;
  classCode: number;
  className: string;
  level: number;
  levelPosition: number;
  levelTotal: number;
  clan: string | null;
  pvp: PvpEntry | null;
};

export type SearchResult = { position: number; name: string; classCode: number; className: string; level: number };
