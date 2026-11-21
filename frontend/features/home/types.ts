import type { ItemShowcase, PublicBanner, RareDrop } from "@/features/site/types";

export type Availability = "unknown" | "online" | "offline" | "partial";

export type RankedCharacter = {
  position: number;
  name: string;
  classCode: number;
  className: string;
  level: number;
};

export type HomeNewsItem = { slug: string; category: string; title: string; summary: string; publishedAtUtc: string };
export type HomeEventItem = { slug: string; name: string; category: string; startsAtUtc: string; endsAtUtc: string | null };
/** Linha do ranking de BP (UserDB.dbo.BattlePoints). */
export type HomePvpEntry = {
  position: number;
  name: string;
  classCode: number;
  className: string;
  battlePoints: number;
  kills: number;
  deaths: number;
  isPvpKing: boolean;
  pvpPoints?: number;
  patent?: number;
};
export type HomeCharacter = { name: string; classCode: number; className: string; level: number | null };
export type HomeClan = {
  name: string;
  members: number;
  points: number;
  wins?: number;
  losses?: number;
  createdUtc?: string | null;
  iconId?: number;
  leader?: HomeCharacter | null;
  sodPosition?: number | null;
};
export type HomeSodPlayer = { name: string; classCode: number; className: string; level: number; points: number; clan: string | null };
/** available false: a fonte falhou. clan null com a fonte no ar: ninguém lidera agora. */
export type HomeClanLeader = { available: boolean; clan: HomeClan | null; topPlayer?: HomeSodPlayer | null };
/**
 * Patente PvP do jogo (1 a 7 pelos pontos PvP, 8 = Rei PvP). minPoints/maxPoints: faixa de pontos PvP
 * (maxPoints null na última; os dois null no Rei). emblem: nome do emblema do cliente em /kit/patentes.
 */
export type HomePvpTier = {
  level: number;
  key: string;
  name: string;
  minPoints: number | null;
  maxPoints: number | null;
  bonus: string;
  emblem: string;
  players: number;
  top: HomePvpEntry | null;
};
export type HomePvpTierLadder = { total: number; tiers: HomePvpTier[] };
export type CommunityLink = { label: string; url: string };

export type HomeSnapshot = {
  server: { name: string; tagline: string; rates: Record<string, string>; communityLinks: CommunityLink[] };
  status: { availability: Availability; checkedAtUtc: string | null; playersOnline: number | null };
  topPlayers: RankedCharacter[];
  rankingAvailable: boolean;
  news: HomeNewsItem[];
  activeEvents: HomeEventItem[];
  upcomingEvents: HomeEventItem[];
  banners: PublicBanner[];
  topBattlePoints?: HomePvpEntry[] | null;
  battlePointsAvailable?: boolean;
  blessCastle?: HomeClanLeader | null;
  sodLeader?: HomeClanLeader | null;
  pvpTiers?: HomePvpTierLadder | null;
  biggestClan?: HomeClanLeader | null;
  rareDrops?: RareDrop[] | null;
  showcase?: ItemShowcase[] | null;
};
