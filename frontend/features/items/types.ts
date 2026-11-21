/** Página pública de um item (arquivos do servidor do jogo). */
export type ItemStat = { label: string; value: string };
export type ItemDropSource = { monster: string; chance: string; choices: number };
export type ItemShopSource = { npc: string; tab: string };
export type ItemGuidePage = {
  id: string;
  name: string;
  image: boolean;
  stats: ItemStat[];
  requirements: ItemStat[];
  spec: ItemStat[];
  drops: ItemDropSource[];
  shops: ItemShopSource[];
};

export const itemCodePattern = /^[A-Za-z]{2}[0-9]{3,4}$/;
