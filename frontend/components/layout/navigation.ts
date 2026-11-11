/** Menu principal. Cada rota ganha sua página nas fases seguintes. */
export const mainNavigation = [
  { href: "/servidor", label: "O Servidor" },
  { href: "/noticias", label: "Notícias" },
  { href: "/eventos", label: "Eventos" },
  { href: "/ranking", label: "Ranking" },
  { href: "/universo", label: "Universo" },
  { href: "/guia", label: "Guia" },
  { href: "/download", label: "Download" },
  { href: "/suporte", label: "Suporte" },
] as const;

export const footerColumns = [
  {
    title: "Jogo",
    links: [
      { href: "/servidor", label: "O Servidor" },
      { href: "/download", label: "Download" },
      { href: "/status", label: "Status do servidor" },
      { href: "/guia", label: "Guia do jogo" },
      { href: "/classes", label: "Classes" },
    ],
  },
  {
    title: "Universo",
    links: [
      { href: "/monstros", label: "Monstros" },
      { href: "/bosses", label: "Bosses" },
      { href: "/mapas", label: "Mapas" },
      { href: "/itens", label: "Itens e equipamentos" },
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
