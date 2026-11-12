/**
 * Menu principal: só páginas que existem. Guia e enciclopédia (monstros, bosses, mapas, itens)
 * entram quando tiverem conteúdo real. O Servidor e Download têm o texto editado no painel.
 */
export const mainNavigation = [
  { href: "/servidor", label: "Servidor" },
  { href: "/noticias", label: "Notícias" },
  { href: "/eventos", label: "Eventos" },
  { href: "/ranking", label: "Ranking" },
  { href: "/classes", label: "Classes" },
  { href: "/personagens", label: "Personagens", wide: true },
  { href: "/status", label: "Status", wide: true },
  { href: "/suporte", label: "Suporte" },
  { href: "/download", label: "Download" },
] as const;

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
