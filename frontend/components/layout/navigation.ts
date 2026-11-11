/**
 * Menu principal: só páginas que existem. O Servidor, Download, Guia e a enciclopédia
 * (monstros, bosses, mapas, itens) entram quando tiverem conteúdo real.
 */
export const mainNavigation = [
  { href: "/noticias", label: "Notícias" },
  { href: "/eventos", label: "Eventos" },
  { href: "/ranking", label: "Ranking" },
  { href: "/classes", label: "Classes" },
  { href: "/personagens", label: "Personagens" },
  { href: "/status", label: "Status" },
  { href: "/suporte", label: "Suporte" },
] as const;

export const footerColumns = [
  {
    title: "Jogo",
    links: [
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
