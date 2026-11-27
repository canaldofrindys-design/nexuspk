import { agingLevels, describeSheltoms } from "./aging";
import type { Guide } from "./types";

/*
 * Guias do NexusPK. Fonte: o wiki do site antigo do servidor (src/pages/wiki: info, dicas, aging,
 * quest_amanda, regras, equipe), revisado e reescrito para o portal. Quando a equipe mudar uma regra
 * ou um sistema do jogo, o texto muda aqui.
 */


export const guides: Guide[] = [
  {
    slug: "primeiros-passos",
    title: "Primeiros passos",
    summary: "Da conta criada ao primeiro boss: o caminho mais curto para começar bem no NexusPK.",
    icon: "mapa",
    scene: "jornada",
    group: "Começando",
    sections: [
      {
        id: "conta-e-jogo",
        title: "Conta e jogo instalado",
        icon: "conta",
        blocks: [
          {
            kind: "steps",
            items: [
              { title: "Crie a sua conta", text: "O cadastro é feito aqui no portal. O login e a senha que você escolher são os mesmos do jogo. Guarde a palavra secreta: ela recupera a conta se você esquecer a senha." },
              { title: "Instale o jogo", text: "Baixe o jogo completo e o update obrigatório e siga o guia de instalação. Leva poucos minutos depois do download." },
              { title: "Confira o status", text: "Antes de entrar, veja na página Status se o login e os canais estão no ar. Manutenções programadas também aparecem lá e nas notícias." },
            ],
          },
          { kind: "links", items: [{ href: "/criar-conta", label: "Criar conta" }, { href: "/instalacao", label: "Guia de instalação" }, { href: "/status", label: "Status do servidor" }] },
        ],
      },
      {
        id: "tribo-e-classe",
        title: "Tribo e classe",
        icon: "classes",
        blocks: [
          { kind: "p", text: "Toda classe pertence a uma de duas tribos. Os Tempskron começam em Ricarten e os Morions em Pillai. As duas tribos se encontram nos mapas, no comércio e nas guerras, então a escolha muda só a cidade inicial e algumas missões." },
          {
            kind: "table",
            caption: "Classes por tribo",
            head: ["Tribo", "Classes", "Cidade inicial"],
            rows: [
              ["Tempskron", "Lutador, Mecânico, Pikeman, Arqueira, Assassina", "Ricarten"],
              ["Morion", "Cavaleiro, Atalanta, Sacerdotisa, Mago, Xamã", "Pillai"],
            ],
          },
          { kind: "p", text: "Quem gosta de linha de frente costuma começar de Cavaleiro, Lutador ou Mecânico. Para jogar à distância, Arqueira e Atalanta. Sacerdotisa é a classe de cura, a mais procurada para grupos. Mago e Xamã batem em área com magia." },
          { kind: "links", items: [{ href: "/classes", label: "Conhecer as classes" }] },
        ],
      },
      {
        id: "primeiras-horas",
        title: "As primeiras horas",
        icon: "personagem",
        blocks: [
          {
            kind: "steps",
            items: [
              { title: "Fale com a Instrutora Amanda", text: "Ela fica em Ricarten e em Pillai e dá sete desafios curtos até o nível 39. As recompensas incluem arma e bracelete com a especialização da sua classe, colar e anéis +2, poções e sheltoms." },
              { title: "Distribua os atributos com calma", text: "Cada classe rende mais com um atributo principal: Força para quem bate corpo a corpo, Agilidade para arqueiras e Atalanta, Espírito para quem usa magia. Na dúvida, siga o requisito dos itens que você quer usar." },
              { title: "Ajuste o jogo", text: "Aperte F10 para abrir as configurações: resolução até 1920×1080, modo janela, som e câmera. O F11 liga e desliga as sombras." },
              { title: "Jogue em grupo", text: "Grupos de até seis personagens dividem a experiência e caçam mais rápido. Use @mensagem para falar só com o grupo e //party nome para convidar alguém de longe." },
            ],
          },
          { kind: "links", items: [{ href: "/guias/missoes-amanda", label: "Missões da Instrutora Amanda" }, { href: "/guias/comandos", label: "Comandos do chat" }] },
        ],
      },
      {
        id: "depois",
        title: "Depois do começo",
        icon: "trofeu",
        blocks: [
          { kind: "list", items: [
            "O nível máximo é 225, e a linha de itens vai até os equipamentos 150B.",
            "Aprimore armas e defesas com Aging (até +26) e Mix: veja o guia de Aging antes de gastar sheltoms.",
            "Bosses deixam itens no próprio mapa e o jogo avisa quando cai um drop raro.",
            "O Bless Castle acontece todo domingo às 16:00 (horário de Brasília).",
          ] },
          { kind: "links", items: [{ href: "/manual", label: "Manual do jogo" }, { href: "/guias/aging", label: "Guia de Aging" }, { href: "/eventos", label: "Agenda de eventos" }] },
        ],
      },
    ],
  },
  {
    slug: "sistemas",
    title: "Sistemas do NexusPK",
    summary: "Tudo o que o servidor tem: nível máximo, itens, arenas, castelo, aging, mix, montarias e mais.",
    icon: "inicio",
    scene: "vale-dourado",
    group: "Começando",
    sections: [
      {
        id: "resumo",
        title: "O servidor em números",
        icon: "inicio",
        blocks: [
          {
            kind: "facts",
            items: [
              { label: "Nível máximo", value: "225" },
              { label: "Linha de itens", value: "até 150B" },
              { label: "Aging máximo", value: "+26" },
              { label: "Falha no Aging", value: "a partir do +24" },
              { label: "Relíquias", value: "4" },
              { label: "Coroas do SoD", value: "3" },
              { label: "Skins", value: "mais de 350" },
              { label: "Bless Castle", value: "domingo, 16:00" },
            ],
          },
          { kind: "note", tone: "info", text: "As taxas de experiência, drop e gold valem as que estiverem na página O Servidor e no painel de status da home: a equipe atualiza por lá." },
        ],
      },
      {
        id: "combate",
        title: "Combate e guerras",
        icon: "pvp",
        blocks: [
          { kind: "list", items: [
            "Arena PvP: fica atrás do baú de Ricarten. Entrou, é combate livre.",
            "Arena Hellsgate.",
            "Bless Castle: a guerra de clãs pelo castelo, todo domingo às 16:00.",
            "SoD com 3 coroas: os três melhores clãs do Survival of the Death ganham as coroas de ouro, prata e bronze.",
            "Pontos de batalha com zonas de segurança, que contam para o ranking PvP.",
          ] },
        ],
      },
      {
        id: "itens",
        title: "Itens e equipamentos",
        icon: "recompensas",
        blocks: [
          { kind: "list", items: [
            "Servidor full drops: a linha de itens cai dos monstros e dos bosses.",
            "Linha de itens até 150B e set de boss com adicionais.",
            "Sistema de cintos e de brincos: dois espaços de equipamento a mais.",
            "4 relíquias para colecionar.",
            "Sistema de elevar item.",
            "Sistema de dissolver itens.",
            "Dois tipos de mix.",
            "Glow nos itens aprimorados, mais de 350 skins e sistema de montarias.",
          ] },
        ],
      },
      {
        id: "mapas",
        title: "Mapas e drops",
        icon: "mapa",
        blocks: [
          { kind: "list", items: [
            "Mapas liberados até o Coração de Fogo.",
            "Mapa VIP1 e Mapa VIP2.",
            "Os bosses deixam itens no próprio mapa.",
            "Alerta de drop: o jogo avisa quando cai um item importante.",
          ] },
        ],
      },
      {
        id: "comunidade",
        title: "Diversão e comunidade",
        icon: "eventos",
        blocks: [
          { kind: "list", items: [
            "Eventos com a equipe, com bosses e premiação em coins.",
            "Roleta com dado e estatísticas.",
            "Sistema de emojis no chat.",
            "Motor gráfico atualizado, com widescreen até 1920×1080.",
          ] },
          { kind: "links", items: [{ href: "/eventos", label: "Agenda de eventos" }, { href: "/ranking", label: "Ranking" }] },
        ],
      },
    ],
  },
  {
    slug: "aging",
    title: "Aging",
    summary: "Quantos sheltoms cada nível pede, uma calculadora do caminho até o seu aging, onde a falha começa e como não perder o item.",
    icon: "trofeu",
    scene: "torre-vigia",
    group: "Evolução",
    sections: [
      {
        id: "como-funciona",
        title: "Como funciona",
        blocks: [
          { kind: "p", text: "O Aging aumenta os atributos de armas e defesas a cada nível. Cada nível pede um conjunto de sheltoms e uma taxa em gold. No NexusPK o aging também vale para botas, luvas e braceletes." },
          { kind: "p", text: "Para colocar um sheltom no aging, clique com o botão direito em cima dele: ele vai direto para a janela, sem arrastar um por um." },
          { kind: "facts", items: [{ label: "Nível máximo", value: "+26" }, { label: "Sem chance de falha", value: "do +1 ao +23" }, { label: "Pode falhar", value: "+24, +25 e +26" }] },
          { kind: "note", tone: "alerta", text: "A partir do +24 o aging pode falhar. Só tente com os sheltoms do nível seguinte já separados e com gold de reserva." },
        ],
      },
      {
        id: "tabela",
        title: "Sheltoms por nível",
        blocks: [
          { kind: "table", caption: "Sheltoms necessários em cada nível de Aging", head: ["Nível", "Sheltoms", "Risco"], rows: agingLevels.map((l) => [`+${l.level}`, describeSheltoms(l.sheltoms), l.risk]) },
          { kind: "p", text: "Ordem dos sheltoms, do mais comum ao mais raro: Fadeo, Sparky, Raident, Transparo, Murky, Devine, Celesto, Mirage, Inferna, Enigma, Bellum e Oredo." },
        ],
      },
      {
        id: "calculadora",
        title: "Calculadora de sheltoms",
        blocks: [
          { kind: "p", text: "Escolha o aging que o item tem hoje e onde você quer chegar: a calculadora soma os sheltoms de todos os níveis do caminho." },
          { kind: "aging-calculator" },
        ],
      },
      {
        id: "dicas",
        title: "Dicas",
        blocks: [
          { kind: "list", items: [
            "Junte os sheltoms do próximo nível antes de começar: o aging segue a sequência e não dá para pular.",
            "Aprimore primeiro a arma: é o item que mais muda o seu dano por gold gasto.",
            "Guarde Mirage e Inferna desde cedo. Eles aparecem em todos os níveis altos.",
          ] },
          { kind: "links", items: [{ href: "/mix", label: "Mix List" }, { href: "/bosses", label: "Horários de boss" }] },
        ],
      },
    ],
  },
  {
    slug: "missoes-amanda",
    title: "Missões da Instrutora Amanda",
    summary: "Os sete desafios para personagens até o nível 39: onde ir, o que caçar e o que você ganha.",
    icon: "noticias",
    scene: "vila-riacho",
    group: "Começando",
    sections: [
      {
        id: "regras",
        title: "Antes de começar",
        blocks: [
          { kind: "p", text: "A Instrutora Amanda fica em Ricarten (Tempskron) e em Pillai (Morion). Os desafios só podem ser recebidos e entregues até o nível 39: passou disso, a missão não conta mais. Os itens de missão vão para o inventário de quests." },
        ],
      },
      {
        id: "desafios",
        title: "Os sete desafios",
        blocks: [
          {
            kind: "table",
            caption: "Desafios da Instrutora Amanda",
            head: ["Desafio", "O que fazer", "Recompensa"],
            rows: [
              ["1. O Vigia Exausto", "Leve o bolo ao vigia do armazém: Vigia Arent (Tempskron) ou Vigia Lavendar (Morion). Volte à Amanda.", "Arma inicial com a especialização da sua classe"],
              ["2. Marcas do Medo", "Cace 5 Hopis no Jardim da Liberdade (Tempskron) ou 5 Coelhos na Estrada dos Ventos (Morion). Volte à Amanda.", "Bracelete Largo com especialização"],
              ["3. Entrega de Doces", "Leve o doce ao Ferreiro Goden (Tempskron) ou ao Ferreiro Bartz (Morion).", "20 poções pequenas de HP, RES e Mana"],
              ["4. Colina dos Duendes", "Cace 5 Duendes Macabros no Jardim da Liberdade (Tempskron) ou 5 Duendes Selvagens na Estrada dos Ventos (Morion). Entregue ao guarda na saída da cidade.", "Colar Precioso +2 e 2 Anéis Preciosos +2 com especialização"],
              ["5. Ameaça Imortal", "Cace 10 Esqueletos no Refúgio dos Anciões (Tempskron) ou 10 Esqueletos do Mal na Floresta dos Espíritos (Morion). Entregue ao Ferreiro Ruga, na Vila Ruinen.", "5 Força Sparky"],
              ["6. Desafio Cíclope", "Cace 10 Ciclopes na Terra Maldita. Volte à Amanda.", "20 poções grandes de HP, RES e Mana"],
              ["7. O Martelo Perdido", "Cace Bargons em Navisko até achar o Martelo Umph e entregue ao Ferreiro Umph, em Navisko.", "5 Sheltoms Transparo"],
            ],
          },
          { kind: "note", tone: "info", text: "Faça os desafios na ordem: as recompensas do começo (arma, bracelete, colar e anéis) deixam os desafios seguintes bem mais rápidos." },
        ],
      },
    ],
  },
  {
    slug: "comandos",
    title: "Comandos do chat",
    summary: "Mensagens de grupo, clã e mapa, conversa privada, convite à distância e ajustes de câmera.",
    icon: "noticias",
    scene: "mercado-entardecer",
    group: "Convivência",
    sections: [
      {
        id: "conversa",
        title: "Conversa",
        blocks: [
          {
            kind: "table",
            head: ["Comando", "O que faz"],
            rows: [
              ["@mensagem", "Fala com todos do seu grupo"],
              ["/CLAN> mensagem", "Fala com todos do seu clã"],
              ["/TRADE> mensagem", "Fala com todos do mapa (comércio)"],
              ["/G mensagem", "Mensagem global para todos que estão online"],
              ["/:jogador mensagem", "Mensagem privada para o jogador"],
              ["/:jogador", "Verifica se o jogador está online"],
              ["/disable_whisper e /enable_whisper", "Bloqueia e libera mensagens privadas"],
            ],
          },
          { kind: "note", tone: "alerta", text: "Use o chat global com moderação: spam no /G é punido (regra 4)." },
        ],
      },
      {
        id: "utilidades",
        title: "Utilidades",
        blocks: [
          {
            kind: "table",
            head: ["Comando ou tecla", "O que faz"],
            rows: [
              ["/Help", "Mostra os comandos do jogo"],
              ["/ConcluirQuests", "Conclui as suas quests de pontos"],
              ["//party jogador", "Convida para o grupo à distância"],
              ["/camera_wave_off e /camera_wave_on", "Trava e destrava o tremor da câmera nas explosões"],
              ["F10", "Configurações do jogo"],
              ["F11", "Liga e desliga as sombras"],
              ["Ctrl + Home ou End", "Captura de tela"],
            ],
          },
        ],
      },
    ],
  },
  {
    slug: "glossario",
    title: "Siglas e glossário",
    summary: "O que querem dizer MS, KS, HS, BC, PT, Spec, Repot e as outras siglas do chat.",
    icon: "guia",
    scene: "vila-conversa",
    group: "Convivência",
    sections: [
      {
        id: "specs",
        title: "Classes e especialização",
        blocks: [
          { kind: "p", text: "Spec é a especialização de um item: um item com a spec da sua classe dá bônus extras. As siglas abaixo aparecem muito no comércio." },
          {
            kind: "table",
            head: ["Sigla", "Significado", "Tribo"],
            rows: [
              ["FS", "Lutador spec", "Tempskron"],
              ["MS", "Mecânico spec", "Tempskron"],
              ["PS", "Pikeman spec", "Tempskron"],
              ["AS", "Arqueira spec", "Tempskron"],
              ["ASS", "Assassina spec", "Tempskron"],
              ["KS", "Cavaleiro spec", "Morion"],
              ["ATA", "Atalanta spec", "Morion"],
              ["PRS", "Sacerdotisa spec", "Morion"],
              ["MGS", "Mago spec", "Morion"],
              ["XM", "Xamã spec", "Morion"],
              ["NS", "Sem spec", "—"],
            ],
          },
        ],
      },
      {
        id: "atributos",
        title: "Atributos do personagem",
        blocks: [
          {
            kind: "table",
            head: ["Sigla", "Atributo"],
            rows: [
              ["HP", "Pontos de vida"],
              ["MP", "Pontos de magia"],
              ["STM", "Resistência (stamina)"],
              ["STR", "Força"],
              ["TAL", "Talento"],
              ["AGI", "Agilidade"],
              ["ABS", "Absorção"],
            ],
          },
        ],
      },
      {
        id: "girias",
        title: "Gírias do jogo",
        blocks: [
          {
            kind: "table",
            head: ["Termo", "Significado"],
            rows: [
              ["EXP / LVL", "Experiência / nível"],
              ["MOB", "Monstro"],
              ["CHAR", "Personagem"],
              ["PT", "Grupo (party). FULL: grupo cheio, com 6"],
              ["PVP / X1", "Jogador contra jogador"],
              ["BC", "Bless Castle, a guerra do castelo"],
              ["KS", "Matar o monstro que outro grupo está caçando"],
              ["HS", "Hell spawn: onde o líder dos monstros aparece"],
              ["SPAWN", "Área onde os monstros nascem sem parar"],
              ["LOOT", "Pegar o drop de outra pessoa"],
              ["POT / REPOT", "Poção / voltar à cidade para comprar poção"],
              ["AGE", "Aging"],
              ["MIX", "Mixagem: colocar sheltoms no item para melhorar a qualidade"],
              ["SOLO", "Quem joga sem grupo"],
              ["PM", "Mensagem privada"],
              ["DC", "Desconexão"],
              ["SS", "Captura de tela"],
              ["GZ", "Parabéns"],
            ],
          },
        ],
      },
    ],
  },
  {
    slug: "regras",
    title: "Regras do servidor",
    summary: "As dez regras de conduta, como a equipe pune e como se proteger de golpes.",
    icon: "guildasBandeira",
    scene: "luar",
    group: "Convivência",
    sections: [
      {
        id: "sobre",
        title: "Sobre as regras",
        blocks: [
          { kind: "p", text: "Ao jogar no NexusPK você aceita estas regras e se compromete a acompanhar as atualizações. Elas são interpretadas e aplicadas pela equipe (moderadores e administradores). Discordar da equipe é permitido, desde que com respeito: se você se sentir prejudicado por alguém da equipe, abra um chamado no suporte." },
          { kind: "p", text: "Uma advertência não é a punição final: é um lembrete para se adequar. A maior parte das regras é bom senso." },
        ],
      },
      {
        id: "conduta",
        title: "Regras de conduta no jogo",
        blocks: [
          {
            kind: "steps",
            items: [
              { title: "Respeito", text: "Não perturbe, humilhe, ameace ou engane outros jogadores. Comportamento infantil também pode ser punido." },
              { title: "Ofensas", text: "Nada de linguagem odiosa, racista ou ameaçadora. Provocação de competição saudável pode; baixo calão não." },
              { title: "Fingir ser da equipe", text: "Nunca se passe por GM ou membro da equipe. Pode levar a banimento permanente." },
              { title: "Chat global", text: "Sem spam ou conversa desnecessária no global." },
              { title: "Trapaças", text: "Proibido usar bots, auto-clicker ou qualquer programa que interfira no jogo, e proibido modificar o cliente." },
              { title: "Abuso de bugs", text: "Não tire vantagem de bugs. Se encontrar um por acidente, avise a equipe pelo suporte na hora." },
              { title: "Golpes", text: "Não use falsas promessas para pegar itens, login ou senha de alguém. Pode resultar em exclusão completa do jogo." },
              { title: "Acusação sem prova", text: "Acusar alguém de bug, VPN ou trapaça sem prova é punível. Mande vídeos e prints para a equipe pelo suporte, que avalia e responde." },
              { title: "Dinheiro real", text: "É proibido vender, comprar ou trocar contas e itens do NexusPK por dinheiro real ou por itens de outros servidores. Dentro do jogo, negocie com gold ou coins." },
              { title: "Propaganda", text: "Não divulgue telefone, endereço ou sites de outros jogos." },
            ],
          },
        ],
      },
      {
        id: "punicoes",
        title: "Como a equipe pune",
        blocks: [
          { kind: "list", items: [
            "Advertência informal: uma mensagem privada da equipe explicando a regra quebrada.",
            "Banimento temporário: de 1 dia a 1 semana ou mais, conforme a gravidade.",
            "Banimento permanente.",
            "Banimento de IP: em casos extremos ou de violações repetidas.",
          ] },
        ],
      },
      {
        id: "seguranca",
        title: "Proteja a sua conta",
        blocks: [
          { kind: "note", tone: "alerta", text: "A equipe nunca pede a sua senha, a sua palavra secreta ou os seus itens, nem no jogo nem fora dele. Quem pedir é golpe: não responda e avise pelo suporte." },
          { kind: "list", items: [
            "Dúvidas são respondidas pelo suporte do portal, não pelo chat do jogo.",
            "Use uma senha que você não usa em nenhum outro site.",
            "Baixe o jogo só pelos links da página Download.",
          ] },
          { kind: "links", items: [{ href: "/suporte", label: "Central de suporte" }, { href: "/recuperar-conta", label: "Recuperar conta" }] },
        ],
      },
    ],
  },
];

export const guideGroups = ["Começando", "Evolução", "Convivência"] as const;

export function guideBySlug(slug: string) {
  return guides.find((g) => g.slug === slug);
}
