import type { KitIconName } from "@/components/ui/KitIcon";

/**
 * Menu principal: só páginas que existem. Guia e enciclopédia (monstros, bosses, mapas, itens)
 * entram quando tiverem conteúdo real. O Servidor e Download têm o texto editado no painel.
 */
export const mainNavigation = [
  { href: "/servidor", label: "Servidor", icon: "inicio" },
  { href: "/noticias", label: "Notícias", icon: "noticias" },
  { href: "/eventos", label: "Eventos", icon: "eventos" },
  { href: "/ranking", label: "Ranking", icon: "trofeu" },
  { href: "/classes", label: "Classes", icon: "classes" },
  { href: "/personagens", label: "Personagens", wide: true, icon: "personagem" },
  { href: "/status", label: "Status", wide: true, icon: "online" },
  { href: "/suporte", label: "Suporte", icon: "guia" },
  { href: "/download", label: "Download", icon: "download" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName; wide?: boolean }[];

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
