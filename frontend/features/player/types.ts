export type PanelCharacter = {
  name: string;
  classCode: number;
  className: string;
  level: number;
  clan: string | null;
  battlePoints: number | null;
  pvpKills: number | null;
  pvpDeaths: number | null;
  rankPosition?: number | null;
  classRankPosition?: number | null;
};

export type PlayerPanel = {
  login: string;
  email: string | null;
  emailConfirmed: boolean;
  emailSendingAvailable: boolean;
  passwordChangeAvailable: boolean;
  gameDataAvailable: boolean;
  game: {
    registeredUtc: string | null;
    blocked: boolean;
    characters: PanelCharacter[];
    vip: { plan: number | null; startsUtc: string | null; endsUtc: string | null; active: boolean } | null;
    coins: number | null;
    rankTotal?: number | null;
  } | null;
  recentLogins: { atUtc: string; ipAddress: string | null }[];
};
