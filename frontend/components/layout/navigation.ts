import type { KitIconName } from "@/components/ui/KitIcon";

/**
 * Menu principal no formato da prancha "home" do kit visual (Início, Cadastro, Download, Ranking,
 * Eventos...). Só páginas que existem: a "Loja" da prancha dá lugar a Notícias e Classes até haver loja.
 * `extra`: aparece só no menu do celular (no PC fica no rodapé e nos atalhos da home).
 */
export const mainNavigation = [
  { href: "/", label: "Início", icon: "inicio" },
  { href: "/criar-conta", label: "Cadastro", icon: "conta" },
  { href: "/download", label: "Download", icon: "download" },
  { href: "/ranking", label: "Ranking", icon: "trofeu" },
  { href: "/eventos", label: "Eventos", icon: "eventos" },
  { href: "/noticias", label: "Notícias", icon: "noticias" },
  { href: "/classes", label: "Classes", icon: "classes" },
  { href: "/suporte", label: "Suporte", icon: "guia" },
  { href: "/servidor", label: "O Servidor", icon: "mapa", extra: true },
  { href: "/personagens", label: "Personagens", icon: "personagem", extra: true },
  { href: "/status", label: "Status", icon: "online", extra: true },
] as const satisfies readonly { href: string; label: string; icon: KitIconName; extra?: boolean }[];

export const footerColumns = [
  {
    title: "Jogo",
    links: [
      { href: "/servidor", label: "O Servidor" },
      { href: "/download", label: "Download" },
      { href: "/status", label: "Status do servidor" },
      { href: "/ranking", label: "Ranking" },
      { href: "/classes", label: "Classes" },
      { href: "/personagens", label: "Buscar personagem" },
      { href: "/eventos", label: "Eventos" },
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
