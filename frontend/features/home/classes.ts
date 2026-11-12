/**
 * Classes jogáveis (códigos 1–10 da ServerDB.dbo.LevelTable). Texto original do portal.
 * `icon`: retrato 84×84 da classe (ranking, perfil, painel). `art`: arte oficial do Priston Tale em public/classes (uso decidido pelo dono do servidor, com crédito).
 * A Guerreira (11) fica de fora até confirmar se está liberada (NexusClasses.ini: EnableNewClasses=0).
 */
export const classes = [
  { code: 1, name: "Lutador", role: "Corpo a corpo, dano alto", art: "/classes/lutador.webp", icon: "/classes/icones/lutador.webp" },
  { code: 2, name: "Mecânico", role: "Corpo a corpo, defesa", art: "/classes/mecanico.webp", icon: "/classes/icones/mecanico.webp" },
  { code: 3, name: "Arqueira", role: "Distância, arcos", icon: "/classes/icones/arqueira.webp" },
  { code: 4, name: "Pikeman", role: "Corpo a corpo, lanças", art: "/classes/pikeman.webp", icon: "/classes/icones/pikeman.webp" },
  { code: 5, name: "Atalanta", role: "Distância, dardos", art: "/classes/atalanta.webp", icon: "/classes/icones/atalanta.webp" },
  { code: 6, name: "Cavaleiro", role: "Corpo a corpo, espadas", art: "/classes/cavaleiro.webp", icon: "/classes/icones/cavaleiro.webp" },
  { code: 7, name: "Mago", role: "Magia ofensiva", art: "/classes/mago.webp", icon: "/classes/icones/mago.webp" },
  { code: 8, name: "Sacerdotisa", role: "Cura e suporte", icon: "/classes/icones/sacerdotisa.webp" },
  { code: 9, name: "Assassina", role: "Corpo a corpo, velocidade", icon: "/classes/icones/assassina.webp" },
  { code: 10, name: "Xamã", role: "Magia e maldições", icon: "/classes/icones/xama.webp" },
] as const;

/** Classe pelo código gravado em LevelTable.Class; `undefined` para código sem ícone (ex.: Guerreira). */
export function classByCode(code: number) {
  return classes.find((c) => c.code === code);
}
