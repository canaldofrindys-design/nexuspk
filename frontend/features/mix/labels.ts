/** Equipamentos da Mix List, na ordem dos filtros da página. Iguais a SiteContentService.MixTargets na API. */
export const mixTargets = {
  armas: "Armas",
  armaduras: "Armaduras",
  robes: "Robes",
  escudos: "Escudos",
  orbs: "Orbs",
  braceletes: "Braceletes",
  luvas: "Luvas",
  botas: "Botas",
  amuletos: "Amuletos",
  aneis: "Anéis",
  brincos: "Brincos",
  cintos: "Cintos",
  outros: "Outros",
} as const satisfies Record<string, string>;

export type MixTarget = keyof typeof mixTargets;

export function mixTargetLabel(key: string): string {
  return (mixTargets as Record<string, string>)[key] ?? "Outros";
}
