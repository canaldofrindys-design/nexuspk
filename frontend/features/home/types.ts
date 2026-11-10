export type Availability = "unknown" | "online" | "offline";

export type RankedCharacter = {
  position: number;
  name: string;
  classCode: number;
  className: string;
  level: number;
};

export type HomeNewsItem = { slug: string; category: string; title: string; summary: string; publishedAtUtc: string };
export type HomeEventItem = { slug: string; name: string; category: string; startsAtUtc: string; endsAtUtc: string | null };
export type CommunityLink = { label: string; url: string };

export type HomeSnapshot = {
  server: { name: string; tagline: string; rates: Record<string, string>; communityLinks: CommunityLink[] };
  status: { availability: Availability; checkedAtUtc: string | null; playersOnline: number | null };
  topPlayers: RankedCharacter[];
  rankingAvailable: boolean;
  news: HomeNewsItem[];
  activeEvents: HomeEventItem[];
  upcomingEvents: HomeEventItem[];
};
