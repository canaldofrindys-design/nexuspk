/**
 * Classes jogáveis (códigos 1–10 da ServerDB.dbo.LevelTable). Texto original do portal.
 * `art`: arte oficial do Priston Tale em public/classes (uso decidido pelo dono do servidor, com crédito).
 * A Guerreira (11) fica de fora até confirmar se está liberada (NexusClasses.ini: EnableNewClasses=0).
 */
export const classes = [
  { code: 1, name: "Lutador", role: "Corpo a corpo, dano alto", art: "/classes/lutador.webp" },
  { code: 2, name: "Mecânico", role: "Corpo a corpo, defesa", art: "/classes/mecanico.webp" },
  { code: 3, name: "Arqueira", role: "Distância, arcos" },
  { code: 4, name: "Pikeman", role: "Corpo a corpo, lanças", art: "/classes/pikeman.webp" },
  { code: 5, name: "Atalanta", role: "Distância, dardos", art: "/classes/atalanta.webp" },
  { code: 6, name: "Cavaleiro", role: "Corpo a corpo, espadas", art: "/classes/cavaleiro.webp" },
  { code: 7, name: "Mago", role: "Magia ofensiva", art: "/classes/mago.webp" },
  { code: 8, name: "Sacerdotisa", role: "Cura e suporte" },
  { code: 9, name: "Assassina", role: "Corpo a corpo, velocidade" },
  { code: 10, name: "Xamã", role: "Magia e maldições" },
] as const;
