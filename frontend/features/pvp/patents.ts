/*
 * Patentes PvP do jogo, na mesma ordem e com os mesmos emblemas do cliente (game\patente\emblems).
 * O nível vem do servidor (campo "patent" do ranking PvP): 1 a 7 pelos pontos PvP, 8 = Rei PvP, 0 = nenhuma.
 */
export type Patent = { level: number; name: string; emblem: string; width: number; height: number };

export const patents: Patent[] = [
  { level: 1, name: "Prata de Elite", emblem: "primal", width: 189, height: 192 },
  { level: 2, name: "Ouro Mestre", emblem: "ceifador", width: 154, height: 192 },
  { level: 3, name: "Guardião de Elite", emblem: "guerreiro", width: 192, height: 168 },
  { level: 4, name: "Mestre Guardião", emblem: "guardiao", width: 161, height: 192 },
  { level: 5, name: "Guerreiro Lendário", emblem: "mestre", width: 190, height: 192 },
  { level: 6, name: "Guerreiro de Elite", emblem: "desafiante", width: 191, height: 192 },
  { level: 7, name: "Mestre Supremo", emblem: "mvp", width: 192, height: 184 },
  { level: 8, name: "Rei PvP", emblem: "rei-pvp", width: 192, height: 157 },
];

export const patentSrc = (emblem: string) => `/kit/patentes/${emblem}.webp`;

export const patentOf = (level: number | null | undefined): Patent | null =>
  level ? (patents.find((p) => p.level === level) ?? null) : null;
