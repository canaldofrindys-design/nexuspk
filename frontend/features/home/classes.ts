/**
 * Classes jogáveis (códigos 1–10 da ServerDB.dbo.LevelTable). Texto original do portal.
 * A Guerreira (11) fica de fora até confirmar se está liberada (NexusClasses.ini: EnableNewClasses=0).
 */
export const classes = [
  { code: 1, name: "Lutador", role: "Corpo a corpo, dano alto" },
  { code: 2, name: "Mecânico", role: "Corpo a corpo, defesa" },
  { code: 3, name: "Arqueira", role: "Distância, arcos" },
  { code: 4, name: "Pikeman", role: "Corpo a corpo, lanças" },
  { code: 5, name: "Atalanta", role: "Distância, dardos" },
  { code: 6, name: "Cavaleiro", role: "Corpo a corpo, espadas" },
  { code: 7, name: "Mago", role: "Magia ofensiva" },
  { code: 8, name: "Sacerdotisa", role: "Cura e suporte" },
  { code: 9, name: "Assassina", role: "Corpo a corpo, velocidade" },
  { code: 10, name: "Xamã", role: "Magia e maldições" },
] as const;
