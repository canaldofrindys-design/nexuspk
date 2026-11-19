import type { KitIconName } from "@/components/ui/KitIcon";

/**
 * Menu principal na ordem da prancha "portal" do kit visual: Início, O Jogo, Download, Cadastro,
 * (Doações), Ranking, Eventos, Suporte. Doações fica de fora até existir a página; no lugar entra Classes.
 * "O Jogo" abre os guias e acende também no Manual e na Instalação (`match`).
 * `extra`: aparece só no menu do celular (no PC fica no rodapé e nos atalhos da home).
 */
export const mainNavigation = [
  { href: "/", label: "Início", icon: "inicio" },
  { href: "/guias", label: "O Jogo", icon: "guia", match: ["/guias", "/manual", "/instalacao"] },
  { href: "/download", label: "Download", icon: "download" },
  { href: "/criar-conta", label: "Cadastro", icon: "conta" },
  { href: "/classes", label: "Classes", icon: "classes" },
  { href: "/ranking", label: "Ranking", icon: "trofeu" },
  { href: "/eventos", label: "Eventos", icon: "eventos" },
  { href: "/suporte", label: "Suporte", icon: "noticias" },
  { href: "/manual", label: "Manual do jogo", icon: "guia", extra: true },
  { href: "/instalacao", label: "Instalação", icon: "download", extra: true },
  { href: "/noticias", label: "Notícias", icon: "noticias", extra: true },
  { href: "/servidor", label: "O Servidor", icon: "mapa", extra: true },
  { href: "/personagens", label: "Personagens", icon: "personagem", extra: true },
  { href: "/status", label: "Status", icon: "online", extra: true },
] as const satisfies readonly { href: string; label: string; icon: KitIconName; extra?: boolean; match?: readonly string[] }[];

/** A página atual pertence a este item do menu? */
export function isActive(item: { href: string; match?: readonly string[] }, path: string) {
  if (item.href === "/") return path === "/";
  return (item.match ?? [item.href]).some((m) => path === m || path.startsWith(`${m}/`));
}

export const footerColumns = [
  {
    title: "Servidor",
    links: [
      { href: "/servidor", label: "O Servidor" },
      { href: "/noticias", label: "Notícias" },
      { href: "/download", label: "Download" },
      { href: "/status", label: "Status do servidor" },
      { href: "/ranking", label: "Ranking" },
      { href: "/classes", label: "Classes" },
      { href: "/personagens", label: "Buscar personagem" },
      { href: "/eventos", label: "Eventos" },
    ],
  },
  {
    title: "O Jogo",
    links: [
      { href: "/guias", label: "Guias" },
      { href: "/manual", label: "Manual do jogo" },
      { href: "/instalacao", label: "Instalação" },
      { href: "/guias/primeiros-passos", label: "Primeiros passos" },
      { href: "/guias/regras", label: "Regras" },
    ],
  },
  {
    title: "Conta",
    links: [
      { href: "/criar-conta", label: "Criar conta" },
      { href: "/login", label: "Entrar" },
      { href: "/recuperar-conta", label: "Recuperar conta" },
      { href: "/suporte", label: "Suporte" },
    ],
  },
] as const;
