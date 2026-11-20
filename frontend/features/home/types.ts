import type { PublicBanner } from "@/features/site/types";

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
};
