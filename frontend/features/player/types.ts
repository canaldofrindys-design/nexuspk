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
  sheet?: CharacterSheet | null;
};

/** Item vestido: lugar (1 a 21 no jogo), nome, ItemCode e o código do arquivo ("WA193") para a foto e o link. */
export type CharacterEquipment = { slot: number; slotName: string; name: string; code: number; fileId: string | null };

/**
 * Ficha do personagem lida do arquivo dele no servidor do jogo, gravado quando ele salva ou sai.
 * experiencePercent: 0 a 100 no nível atual (null no nível máximo). savedUtc: hora em que o servidor gravou.
 */
export type CharacterSheet = {
  name: string;
  level: number;
  experience: number;
  experiencePercent: number | null;
  strength: number;
  spirit: number;
  talent: number;
  agility: number;
  health: number;
  statPoints: number;
  attackRating: number;
  damageMin: number;
  damageMax: number;
  attackSpeed: number;
  critical: number;
  defense: number;
  block: number;
  absorption: number;
  moveSpeed: number;
  mana: number;
  stamina: number;
  weight: number;
  maxWeight: number;
  gold: number;
  resistances: { organic: number; fire: number; ice: number; lightning: number; poison: number };
  mapId: number | null;
  mapName: string | null;
  savedUtc: string;
  equipment: CharacterEquipment[];
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
