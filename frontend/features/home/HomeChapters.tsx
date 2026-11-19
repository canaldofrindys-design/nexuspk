import Link from "next/link";
import { SceneImage } from "@/components/scene/SceneImage";
import { scenes, type SceneKey } from "@/components/scene/scenes";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";

/**
 * Capítulos da home: cada um apresenta uma parte do portal sobre um cenário pintado. O fundo do
 * site troca para o mesmo cenário quando o capítulo chega ao meio da tela (SceneBackdrop), a
 * gravura emoldurada se move em parallax e os itens entram em sequência ao rolar.
 *
 * Os textos descrevem só o que o portal realmente oferece (páginas e recursos existentes);
 * taxas, horários e números ficam com a API e com o painel da equipe.
 */
type Item = { icon: KitIconName; title: string; text: string; href: string; cta: string };

type Chapter = {
  id: string;
  numeral: string;
  scene: SceneKey;
  eyebrow: string;
  icon: KitIconName;
  title: string;
  lead: string;
  caption: string;
  items: Item[];
  actions: { href: string; label: string; primary?: boolean }[];
  steps?: boolean;
};

const chapters: Chapter[] = [
  {
    id: "comecar",
    numeral: "I",
    scene: "jornada",
    eyebrow: "Primeiros passos",
    icon: "mapa",
    title: "Comece a jogar em quatro passos",
    lead: "Do cadastro ao primeiro monstro derrotado leva poucos minutos. A conta do site é a mesma do jogo, então tudo o que você fizer aqui vale lá dentro.",
    caption: "A estrada para a cidade: todo aventureiro começa do mesmo jeito.",
    steps: true,
    items: [
      {
        icon: "conta",
        title: "Crie sua conta",
        text: "Cadastro gratuito com confirmação por e-mail. Guarde bem o e-mail: é por ele que você recupera a conta se esquecer a senha.",
        href: "/criar-conta",
        cta: "Criar conta",
      },
      {
        icon: "download",
        title: "Baixe o cliente",
        text: "O cliente completo, o launcher e as atualizações ficam na página de download, com versão, tamanho e requisitos de cada arquivo.",
        href: "/download",
        cta: "Ir para o download",
      },
      {
        icon: "classes",
        title: "Escolha a sua classe",
        text: "São dez classes, do Lutador à Xamã. Veja a ilustração e o ranking de cada uma antes de decidir com quem vai começar.",
        href: "/classes",
        cta: "Conhecer as classes",
      },
      {
        icon: "online",
        title: "Entre no servidor",
        text: "Confira se o login e os canais estão no ar na página de status. Todos os horários do portal seguem o horário de Brasília.",
        href: "/status",
        cta: "Ver o status",
      },
    ],
    actions: [
      { href: "/criar-conta", label: "Criar conta grátis", primary: true },
      { href: "/download", label: "Baixar o jogo" },
    ],
  },
  {
    id: "batalhas",
    numeral: "II",
    scene: "luar",
    eyebrow: "Eventos, bosses e PvP",
    icon: "boss",
    title: "Quando a lua sobe, a guerra começa",
    lead: "A agenda pública mostra o que está acontecendo agora, o que vem a seguir e o que acabou de terminar. Cada evento tem contagem regressiva, lembrete por e-mail e arquivo para o seu calendário.",
    caption: "A trilha de cristais sob a lua cheia, caminho dos que buscam os bosses.",
    items: [
      {
        icon: "boss",
        title: "Bosses mundiais",
        text: "Chefes com horário marcado na agenda. Ative o lembrete e chegue com o grupo montado antes do surgimento.",
        href: "/eventos?categoria=boss",
        cta: "Agenda de bosses",
      },
      {
        icon: "pvp",
        title: "Arena PvP",
        text: "Ranking de batalha por pontos (BP) e abates. Cada personagem mostra o seu histórico de PvP no perfil público.",
        href: "/ranking?tipo=pvp",
        cta: "Ranking PvP",
      },
      {
        icon: "guilda",
        title: "Guerra de clãs",
        text: "Os clãs disputam o topo do ranking próprio. Reúna a sua guilda e leve o nome dela para a primeira página.",
        href: "/ranking?tipo=clas",
        cta: "Ranking de clãs",
      },
      {
        icon: "recompensas",
        title: "Eventos especiais",
        text: "Invasões, eventos de EXP e de drop e datas comemorativas, com calendário do mês e filtro por tipo de evento.",
        href: "/eventos?categoria=especial",
        cta: "Ver eventos",
      },
    ],
    actions: [
      { href: "/eventos", label: "Abrir a agenda", primary: true },
      { href: "/ranking?tipo=pvp", label: "Ranking PvP" },
    ],
  },
  {
    id: "comunidade",
    numeral: "III",
    scene: "mercado-entardecer",
    eyebrow: "Notícias e comunidade",
    icon: "noticias",
    title: "A vila se encontra na praça",
    lead: "Atualizações, manutenções e novidades são publicadas pela equipe no portal antes de qualquer outro lugar. Os canais oficiais da comunidade ficam reunidos na página do servidor.",
    caption: "A feira da vila ao pôr do sol: ponto de encontro de mercadores e aventureiros.",
    items: [
      {
        icon: "noticias",
        title: "Notícias e atualizações",
        text: "Notas de atualização, avisos de manutenção e novidades, com data de publicação e filtro por categoria.",
        href: "/noticias",
        cta: "Ler as notícias",
      },
      {
        icon: "guildasBandeira",
        title: "Canais oficiais",
        text: "Links da comunidade cadastrados pela equipe, junto da apresentação e das taxas do servidor.",
        href: "/servidor",
        cta: "Conhecer o servidor",
      },
      {
        icon: "personagem",
        title: "Perfil de personagem",
        text: "Procure qualquer personagem pelo nome e veja classe, nível, posição no ranking e resultados de PvP.",
        href: "/personagens",
        cta: "Buscar personagem",
      },
      {
        icon: "trofeu",
        title: "Rankings ao vivo",
        text: "Geral por nível, por classe, de clãs e de PvP, além do ganho de níveis do dia, da semana e do mês.",
        href: "/ranking",
        cta: "Ver os rankings",
      },
    ],
    actions: [
      { href: "/noticias", label: "Últimas notícias", primary: true },
      { href: "/servidor", label: "O servidor" },
    ],
  },
  {
    id: "seguranca",
    numeral: "IV",
    scene: "torre-vigia",
    eyebrow: "Status, conta e suporte",
    icon: "online",
    title: "Um servidor vigiado de perto",
    lead: "Da torre se vê o vale inteiro. O portal mostra o estado do login e dos canais, avisa as manutenções com antecedência e mantém um canal direto com a equipe para cada conta.",
    caption: "A torre de vigia à beira do penhasco guarda a entrada da vila.",
    items: [
      {
        icon: "online",
        title: "Status ao vivo",
        text: "Login e canais verificados continuamente, com o histórico das últimas horas e as próximas manutenções programadas.",
        href: "/status",
        cta: "Status do servidor",
      },
      {
        icon: "conta",
        title: "Conta protegida",
        text: "Proteção contra robôs no cadastro e na recuperação, troca de senha pelo painel e lista dos acessos recentes ao site.",
        href: "/painel",
        cta: "Painel do jogador",
      },
      {
        icon: "guia",
        title: "Suporte por chamado",
        text: "Abra um chamado com até três imagens anexadas e acompanhe cada resposta da equipe pelo painel.",
        href: "/suporte",
        cta: "Central de suporte",
      },
      {
        icon: "recompensas",
        title: "A equipe nunca pede sua senha",
        text: "Nem no chamado, nem no jogo, nem nas redes. Desconfie de qualquer mensagem que peça a senha em nome do NexusPK.",
        href: "/recuperar-conta",
        cta: "Recuperar conta",
      },
    ],
    actions: [
      { href: "/status", label: "Ver o status", primary: true },
      { href: "/suporte", label: "Falar com a equipe" },
    ],
  },
];

function ChapterArt({ chapter, flip }: { chapter: Chapter; flip: boolean }) {
  return (
    <figure data-reveal={flip ? "right" : "left"} className="chapter-art relative">
      <div className="game-window overflow-hidden p-1.5">
        <div className="px-scope relative aspect-[4/3] overflow-hidden rounded-[9px] lg:aspect-[5/4]">
          <div className="px-layer absolute inset-x-0 -inset-y-[14%]" style={{ "--px-depth": "9%" } as React.CSSProperties}>
            <SceneImage
              scene={chapter.scene}
              alt={scenes[chapter.scene].alt}
              sizes="(min-width: 1024px) 560px, 100vw"
              className="chapter-art__img size-full object-cover"
            />
          </div>
          <div aria-hidden className="chapter-art__light" />
          <div aria-hidden className="absolute inset-0 bg-gradient-to-t from-night/85 via-transparent to-transparent" />
          <figcaption className="absolute inset-x-0 bottom-0 flex items-end gap-3 p-4 text-sm text-silver/90">
            <span className="font-display text-3xl leading-none text-gold-bright/90">{chapter.numeral}</span>
            <span className="[text-shadow:0_1px_4px_rgb(0_0_0/0.9)]">{chapter.caption}</span>
          </figcaption>
        </div>
      </div>
      <span aria-hidden className="chapter-art__corner chapter-art__corner--tl" />
      <span aria-hidden className="chapter-art__corner chapter-art__corner--br" />
    </figure>
  );
}

function ChapterBody({ chapter }: { chapter: Chapter }) {
  const titleId = `cap-${chapter.id}`;
  return (
    <div className="min-w-0">
      <p data-reveal="up" className="flex items-center gap-2 font-display text-xs tracking-[0.28em] text-gold-bright uppercase">
        <KitIcon name={chapter.icon} size={32} />
        {chapter.eyebrow}
      </p>
      <h2 id={titleId} data-reveal="up" className="text-gilded mt-2 font-display text-3xl leading-tight font-bold sm:text-4xl">
        {chapter.title}
      </h2>
      <p data-reveal="up" className="mt-3 max-w-2xl leading-relaxed text-silver/85">
        {chapter.lead}
      </p>
      <ol className="mt-6 grid gap-3 sm:grid-cols-2">
        {chapter.items.map((item, i) => (
          <li key={item.title} data-reveal="up" className="chapter-card glass-panel group relative rounded-[10px] p-4">
            <div className="flex items-start gap-3">
              <span className="chapter-card__icon relative shrink-0">
                <KitIcon name={item.icon} size={40} />
                {chapter.steps ? (
                  <span aria-hidden className="absolute -right-1.5 -bottom-1 grid size-5 place-items-center rounded-full border border-gold-bright bg-medieval font-display text-[11px] font-bold text-gold-bright">
                    {i + 1}
                  </span>
                ) : null}
              </span>
              <div className="min-w-0">
                <h3 className="font-display text-[15px] font-bold tracking-wide text-gold-bright">
                  {chapter.steps ? <span className="sr-only">Passo {i + 1}: </span> : null}
                  {item.title}
                </h3>
                <p className="mt-1 text-sm leading-relaxed text-silver/80">{item.text}</p>
                <Link href={item.href} className="mt-2 inline-flex items-center gap-1 text-sm font-semibold text-arcane after:absolute after:inset-0 after:rounded-[10px] hover:underline">
                  {item.cta}
                  <span aria-hidden className="transition-transform group-hover:translate-x-1">→</span>
                </Link>
              </div>
            </div>
          </li>
        ))}
      </ol>
      <div data-reveal="up" className="mt-6 flex flex-wrap gap-3">
        {chapter.actions.map((a) =>
          a.primary ? (
            <Link key={a.href} href={a.href} className="nx-btn btn-glow inline-flex items-center px-5 py-2.5 font-display text-sm font-bold tracking-wide uppercase">
              {a.label}
            </Link>
          ) : (
            <Link
              key={a.href}
              href={a.href}
              className="inline-flex items-center rounded-[9px] border border-gold-bright/50 bg-night/50 px-5 py-2.5 font-display text-sm font-bold tracking-wide text-gold-bright uppercase transition-colors hover:bg-night/80"
            >
              {a.label}
            </Link>
          ),
        )}
      </div>
    </div>
  );
}

export function HomeChapters() {
  return (
    <div className="flex flex-col">
      {chapters.map((c, i) => {
        const flip = i % 2 === 1;
        return (
          <section key={c.id} id={c.id} data-scene={c.scene} aria-labelledby={`cap-${c.id}`} className="chapter relative scroll-mt-20 py-14 sm:py-20">
            <span aria-hidden className="chapter__numeral">{c.numeral}</span>
            <div className={`mx-auto grid max-w-7xl items-center gap-8 px-4 lg:gap-12 ${flip ? "lg:grid-cols-[minmax(0,1fr)_minmax(0,0.9fr)]" : "lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1fr)]"}`}>
              <div className={flip ? "lg:order-2" : undefined}>
                <ChapterArt chapter={c} flip={flip} />
              </div>
              <ChapterBody chapter={c} />
            </div>
          </section>
        );
      })}
    </div>
  );
}
