import type { Section } from "./types";

/**
 * Manual do jogo: referência em capítulos, do teclado ao castelo. Fonte: wiki do site antigo
 * (info, dicas, forces e regras), reescrito. Os guias detalhados ficam em /guias/<slug>.
 */
export const manual: Section[] = [
  {
    id: "teclas",
    title: "Interface e teclas",
    icon: "personagem",
    blocks: [
      { kind: "p", text: "O NexusPK roda em widescreen, com resolução até 1920×1080. Tudo o que é visual se ajusta dentro do jogo, sem mexer em arquivos." },
      {
        kind: "table",
        head: ["Tecla", "Função"],
        rows: [
          ["F10", "Configurações: resolução, modo janela, som e câmera"],
          ["F11", "Liga e desliga as sombras (ganha desempenho em PC modesto)"],
          ["Ctrl + Home ou End", "Captura de tela"],
          ["Enter", "Abre o chat"],
        ],
      },
      { kind: "links", items: [{ href: "/guias/comandos", label: "Todos os comandos do chat" }] },
    ],
  },
  {
    id: "personagem",
    title: "Personagem e atributos",
    icon: "classes",
    blocks: [
      { kind: "p", text: "São dez classes divididas em duas tribos. Os Tempskron (Lutador, Mecânico, Pikeman, Arqueira e Assassina) começam em Ricarten; os Morions (Cavaleiro, Atalanta, Sacerdotisa, Mago e Xamã) começam em Pillai." },
      {
        kind: "table",
        head: ["Atributo", "Para que serve"],
        rows: [
          ["Força (STR)", "Dano corpo a corpo e requisito de armas pesadas"],
          ["Espírito", "Dano e mana de quem usa magia"],
          ["Talento (TAL)", "Requisito de itens e precisão"],
          ["Agilidade (AGI)", "Dano à distância, defesa e velocidade"],
          ["Vitalidade", "Vida (HP)"],
        ],
      },
      { kind: "p", text: "Itens com a especialização (spec) da sua classe dão bônus extras. Antes de comprar, confira se o item é da sua spec: a sigla aparece no anúncio (FS, KS, PRS…)." },
      { kind: "links", items: [{ href: "/classes", label: "As dez classes" }, { href: "/guias/glossario", label: "Siglas de spec" }] },
    ],
  },
  {
    id: "evolucao",
    title: "Evolução",
    icon: "trofeu",
    blocks: [
      { kind: "facts", items: [{ label: "Nível máximo", value: "225" }, { label: "Linha de itens", value: "até 150B" }, { label: "Missões iniciais", value: "até o nível 39" }] },
      { kind: "p", text: "Comece pelos desafios da Instrutora Amanda e passe a caçar em grupo assim que puder. Do meio do jogo em diante, os mapas VIP e os bosses de mapa são as fontes de experiência e de itens mais rápidas." },
      { kind: "p", text: "O comando /ConcluirQuests conclui as suas quests de pontos de uma vez." },
      { kind: "links", items: [{ href: "/guias/missoes-amanda", label: "Missões da Instrutora Amanda" }, { href: "/ranking", label: "Ranking de nível" }] },
    ],
  },
  {
    id: "itens",
    title: "Itens e equipamentos",
    icon: "recompensas",
    blocks: [
      { kind: "p", text: "O servidor é full drops: a linha de itens até 150B cai dos monstros e dos bosses. O set de boss vem com adicionais." },
      {
        kind: "table",
        head: ["Sistema", "Como usar"],
        rows: [
          ["Cintos e brincos", "Dois espaços de equipamento a mais, com atributos próprios"],
          ["Relíquias", "Quatro relíquias para colecionar"],
          ["Elevar item", "Sobe o equipamento para a categoria seguinte"],
          ["Dissolver", "Desfaz itens que você não vai usar"],
          ["Glow e skins", "Brilho nos itens aprimorados e mais de 350 visuais"],
          ["Montarias", "Montarias para percorrer os mapas"],
        ],
      },
    ],
  },
  {
    id: "aprimoramento",
    title: "Aging e mix",
    icon: "loja",
    blocks: [
      { kind: "p", text: "O Aging sobe os atributos do item nível por nível, até +26, e vale também para botas, luvas e braceletes. Do +1 ao +23 não há falha; a partir do +24 o aging pode falhar. Clique com o botão direito no sheltom para mandá-lo direto para a janela de aging." },
      { kind: "p", text: "O Mix coloca sheltoms no item para melhorar a qualidade. O NexusPK tem dois tipos de mix." },
      { kind: "links", items: [{ href: "/guias/aging", label: "Tabela completa de Aging" }] },
    ],
  },
  {
    id: "mapas",
    title: "Mapas e drops",
    icon: "mapa",
    blocks: [
      { kind: "list", items: [
        "Mapas liberados até o Coração de Fogo, incluindo o Lago da Ganância.",
        "Mapa VIP1 e Mapa VIP2.",
        "Os bosses deixam itens no próprio mapa, e o alerta de drop avisa quando cai algo importante.",
      ] },
      { kind: "note", tone: "info", text: "Pegar o drop de outro jogador (loot) e roubar o monstro que outro grupo está caçando (KS) são malvistos e podem virar denúncia. Combine com o grupo antes." },
    ],
  },
  {
    id: "pvp",
    title: "PvP e guerras",
    icon: "pvp",
    blocks: [
      {
        kind: "table",
        head: ["Onde", "Quando", "Como funciona"],
        rows: [
          ["Arena PvP", "Sempre aberta", "Atrás do baú de Ricarten. Combate livre entre jogadores"],
          ["Arena Hellsgate", "Conforme a agenda", "Arena de desafio do servidor"],
          ["Bless Castle", "Domingo, 16:00", "Guerra de clãs pela posse do castelo"],
          ["Survival of the Death (SoD)", "Conforme a agenda", "Os três melhores clãs ganham as coroas de ouro, prata e bronze, com bônus de HP"],
        ],
      },
      { kind: "p", text: "Os pontos de batalha contam para o ranking PvP. As zonas de segurança protegem quem não está na área de combate." },
      { kind: "links", items: [{ href: "/ranking?tipo=pvp", label: "Ranking PvP" }, { href: "/ranking?tipo=clas", label: "Ranking de clãs" }, { href: "/eventos", label: "Agenda" }] },
    ],
  },
  {
    id: "eventos",
    title: "Eventos",
    icon: "eventos",
    blocks: [
      { kind: "p", text: "A equipe faz eventos com bosses e premiação em coins. A roleta com dado tem estatísticas de quem mais ganhou. Datas e horários saem na agenda de eventos, sempre em horário de Brasília, e você pode pedir um lembrete por e-mail." },
      { kind: "links", items: [{ href: "/eventos", label: "Agenda de eventos" }, { href: "/noticias", label: "Notícias" }] },
    ],
  },
  {
    id: "conta",
    title: "Conta e segurança",
    icon: "conta",
    blocks: [
      { kind: "list", items: [
        "Login e senha do portal são os mesmos do jogo.",
        "Esqueceu a senha? Recupere pelo portal com o e-mail e a palavra secreta.",
        "A equipe nunca pede senha, palavra secreta ou itens.",
        "Venda de contas ou itens por dinheiro real é proibida e leva a banimento.",
      ] },
      { kind: "links", items: [{ href: "/painel", label: "Painel da conta" }, { href: "/recuperar-conta", label: "Recuperar conta" }, { href: "/guias/regras", label: "Regras" }] },
    ],
  },
];
