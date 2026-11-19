import type { Section } from "./types";

/**
 * Guia de instalação. Os links de download vêm do painel (Conteúdo do site > Downloads); quando o
 * painel ainda não tem nenhum, a página mostra os links oficiais publicados no site antigo.
 */
export const fallbackDownloads = [
  {
    name: "Jogo completo (versão 5)",
    description: "Instalação completa do NexusPK, compactada.",
    size: "5,3 GB",
    href: "https://drive.google.com/file/d/1CNpm11ceF3uyCxbcShU91TUQsy8FDHaq/view?usp=sharing",
    host: "Google Drive",
  },
  {
    name: "Update obrigatório (NexusPK Patch Launcher)",
    description: "Atualiza o jogo para a versão do servidor. Sem ele o login não abre.",
    size: "",
    href: "https://www.mediafire.com/file/c76jjz6cpdpccq4/NexusPK-Patch-Launcher.zip/file",
    host: "Mediafire",
  },
] as const;

export const requirements = {
  head: ["", "Mínimo", "Recomendado"],
  rows: [
    ["Sistema", "Windows 10 (64 bits)", "Windows 10 ou 11 (64 bits)"],
    ["Processador", "Dual-core de 2 GHz", "Quad-core de 3 GHz"],
    ["Memória", "4 GB", "8 GB"],
    ["Vídeo", "Placa com DirectX 9.0c e 1 GB", "Placa dedicada com 2 GB"],
    ["Espaço livre", "15 GB (arquivo + jogo instalado)", "20 GB em SSD"],
    ["Internet", "Banda larga de 5 Mbps", "Cabo ou Wi-Fi 5 GHz"],
  ],
};

export const runtimes = [
  { href: "https://www.microsoft.com/pt-br/download/details.aspx?id=35", label: "DirectX (runtime para usuário final)", text: "Resolve erros de d3dx9_xx.dll e tela preta ao abrir." },
  { href: "https://learn.microsoft.com/pt-br/cpp/windows/latest-supported-vc-redist", label: "Visual C++ Redistributable (x86 e x64)", text: "Resolve erros de MSVCP e VCRUNTIME. Instale as duas versões." },
  { href: "https://www.microsoft.com/pt-br/download/details.aspx?id=17851", label: ".NET Framework 4", text: "Usado pelo launcher. No Windows 10 e 11 normalmente já vem instalado." },
  { href: "https://www.nvidia.com/pt-br/drivers/", label: "Driver de vídeo NVIDIA", text: "Mantenha o driver da placa de vídeo atualizado." },
  { href: "https://www.amd.com/pt/support/download/drivers.html", label: "Driver de vídeo AMD", text: "Para placas e processadores com vídeo AMD." },
];

export const installSteps: Section[] = [
  {
    id: "passo-a-passo",
    title: "Passo a passo",
    icon: "download",
    blocks: [
      {
        kind: "steps",
        items: [
          { title: "Crie a sua conta", text: "Cadastre-se no portal. O login e a senha valem para o jogo." },
          { title: "Baixe o jogo completo e o update", text: "Baixe os dois arquivos da lista ao lado. O jogo completo tem cerca de 5,3 GB: use uma conexão estável e evite baixar pelo celular." },
          { title: "Extraia o jogo", text: "Clique com o botão direito no arquivo e extraia com o 7-Zip ou o WinRAR para uma pasta simples, como C:\\NexusPK. Evite \"Arquivos de Programas\" e a Área de Trabalho: o Windows bloqueia a gravação de atualizações nessas pastas." },
          { title: "Instale os componentes", text: "Instale o DirectX e o Visual C++ (links abaixo) e reinicie o computador se o instalador pedir." },
          { title: "Aplique o update obrigatório", text: "Extraia o NexusPK Patch Launcher dentro da pasta do jogo e aceite substituir os arquivos." },
          { title: "Abra o launcher e jogue", text: "Abra o launcher (com o botão direito, \"Executar como administrador\" na primeira vez), espere a atualização terminar e clique em Jogar. Entre com o seu login, escolha o canal e crie o personagem." },
        ],
      },
      { kind: "note", tone: "info", text: "Primeira vez no jogo? Aperte F10 para escolher a resolução (até 1920×1080) e o modo janela." },
    ],
  },
  {
    id: "antivirus",
    title: "Antivírus e anti-hack",
    icon: "conta",
    blocks: [
      { kind: "p", text: "O NexusPK tem anti-hack próprio, e alguns antivírus marcam o launcher ou o anti-hack como suspeitos por engano, ou movem arquivos para a quarentena. Se o jogo fechar logo ao abrir ou faltar arquivo depois da instalação, restaure o arquivo da quarentena e adicione a pasta do jogo às exceções do antivírus." },
      { kind: "note", tone: "alerta", text: "Faça isso apenas com o jogo baixado pelos links oficiais desta página. Nunca use launchers, \"patches\" ou programas enviados por outros jogadores: além de risco à conta, é trapaça (regra 5)." },
    ],
  },
  {
    id: "problemas",
    title: "Problemas comuns",
    icon: "guia",
    blocks: [
      {
        kind: "table",
        head: ["O que acontece", "Como resolver"],
        rows: [
          ["Erro de d3dx9_43.dll ou outra d3dx9", "Instale o DirectX (runtime para usuário final)."],
          ["Erro de MSVCP140.dll ou VCRUNTIME140.dll", "Instale o Visual C++ Redistributable x86 e x64."],
          ["O launcher não atualiza ou trava em 0%", "Execute como administrador, libere o launcher no firewall do Windows e confira se a pasta não está em Arquivos de Programas."],
          ["O jogo fecha logo ao abrir", "O antivírus provavelmente removeu um arquivo. Restaure da quarentena, adicione a pasta às exceções e aplique o update de novo."],
          ["Tela preta ou imagem cortada", "Aperte F10 e troque a resolução ou ative o modo janela. Atualize o driver de vídeo."],
          ["Lento ou com travadas", "Desligue as sombras com F11 e feche programas pesados em segundo plano."],
          ["Não consigo entrar (login)", "Veja a página Status: o login pode estar em manutenção. Se esqueceu a senha, use Recuperar conta."],
        ],
      },
      { kind: "links", items: [{ href: "/status", label: "Status do servidor" }, { href: "/recuperar-conta", label: "Recuperar conta" }, { href: "/suporte", label: "Abrir chamado no suporte" }] },
    ],
  },
];
