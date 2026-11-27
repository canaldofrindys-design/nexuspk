/** Banco de monstros público (arquivos Monster\*.inf do servidor do jogo). */
export type MonsterSummary = { slug: string; name: string; level: number | null; hp: number | null; items: number; gold: boolean };
export type MonsterList = { available: boolean; total: number; monsters: MonsterSummary[] };
export type MonsterDropItem = { id: string; name: string; image: boolean; perKill: string | null };
export type MonsterDropLine = { chance: string; choices: number; gold: string | null; items: MonsterDropItem[] };
export type MonsterPage = { slug: string; name: string; level: number | null; hp: number | null; exp: number | null; drops: MonsterDropLine[] };

/** "0.33" (ponto, como vem do arquivo) para "0,33%". */
export function percent(value: string) {
  return `${value.replace(".", ",")}%`;
}
