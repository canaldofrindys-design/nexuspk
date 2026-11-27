import type { SceneKey } from "@/components/scene/scenes";
import type { KitIconName } from "@/components/ui/KitIcon";

/** Blocos de conteúdo de um guia. Texto puro: nada vira HTML. */
export type Block =
  | { kind: "p"; text: string }
  | { kind: "list"; items: string[] }
  | { kind: "steps"; items: { title: string; text: string }[] }
  | { kind: "table"; caption?: string; head: string[]; rows: string[][] }
  | { kind: "facts"; items: { label: string; value: string }[] }
  | { kind: "note"; tone: "info" | "alerta"; text: string }
  | { kind: "links"; items: { href: string; label: string; text?: string }[] }
  /** Calculadora de sheltoms do Aging (componente do navegador). */
  | { kind: "aging-calculator" };

export type Section = { id: string; title: string; icon?: KitIconName; blocks: Block[] };

export type Guide = {
  slug: string;
  title: string;
  /** Uma linha: aparece no card da página Guias e abaixo do título. */
  summary: string;
  icon: KitIconName;
  scene: SceneKey;
  /** Grupo no índice da página Guias. */
  group: "Começando" | "Evolução" | "Convivência";
  sections: Section[];
};
