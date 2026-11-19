/**
 * Cenários pintados do NexusPK (public/art/cenas), em WebP com três larguras cada.
 * Cada cena tem um lugar fixo no portal, escolhido pelo que a arte mostra:
 *
 * | Cena                 | O que mostra                                    | Onde aparece                         |
 * | -------------------- | ----------------------------------------------- | ------------------------------------ |
 * | `vale-dourado`       | vale ao entardecer, ponte, portal de cristal     | abertura da home, Ranking, Personagens |
 * | `luar`               | trilha de cristais e castelo sob a lua cheia    | Eventos, PvP, Bosses, Classes        |
 * | `jornada`            | aventureiro seguindo a estrada para a cidade    | "Comece em 3 passos", Download, Conta |
 * | `mercado-entardecer` | feira da vila ao pôr do sol                     | Notícias, comunidade da home         |
 * | `vila-conversa`      | moradores e aventureiros conversando            | Suporte, avisos e agenda da home     |
 * | `vila-riacho`        | vila com riacho, ponte e guardas                | O Servidor, Painel do jogador        |
 * | `torre-vigia`        | torre de vigia à beira do penhasco              | Status do servidor, segurança        |
 */
export type SceneKey = "vale-dourado" | "luar" | "jornada" | "mercado-entardecer" | "vila-conversa" | "vila-riacho" | "torre-vigia";

export type Scene = {
  key: SceneKey;
  /** Descrição para leitores de tela quando a cena aparece como imagem de conteúdo. */
  alt: string;
  width: number;
  height: number;
  /** Larguras geradas (a maior é a original reduzida, nunca ampliada). */
  widths: readonly number[];
  /** Ponto de interesse para object-position (o que não pode ser cortado). */
  focus: string;
};

export const scenes: Record<SceneKey, Scene> = {
  "vale-dourado": {
    key: "vale-dourado",
    alt: "Vale ao entardecer com casas de pedra, um riacho sob a ponte de madeira e um portal de cristal azul nas montanhas",
    width: 1600,
    height: 900,
    widths: [640, 1024, 1600],
    focus: "60% 45%",
  },
  luar: {
    key: "luar",
    alt: "Trilha iluminada por cristais azuis até as ruínas de um portal, com o castelo sob a lua cheia ao fundo",
    width: 1600,
    height: 900,
    widths: [640, 1024, 1600],
    focus: "55% 40%",
  },
  jornada: {
    key: "jornada",
    alt: "Aventureiro de armadura azul seguindo a estrada de terra em direção à cidade e ao cristal na montanha",
    width: 1448,
    height: 1086,
    widths: [640, 1024, 1448],
    focus: "60% 55%",
  },
  "mercado-entardecer": {
    key: "mercado-entardecer",
    alt: "Feira da vila ao pôr do sol, com barracas, lampiões e aventureiros chegando pela estrada",
    width: 1448,
    height: 1086,
    widths: [640, 1024, 1448],
    focus: "55% 50%",
  },
  "vila-conversa": {
    key: "vila-conversa",
    alt: "Aventureiros conversando com um morador idoso na praça da vila, com cachoeira e moinho ao fundo",
    width: 1448,
    height: 1086,
    widths: [640, 1024, 1448],
    focus: "55% 55%",
  },
  "vila-riacho": {
    key: "vila-riacho",
    alt: "Vila com casas de telhado laranja, guardas na entrada e um riacho atravessado por uma ponte",
    width: 1448,
    height: 1086,
    widths: [640, 1024, 1448],
    focus: "45% 45%",
  },
  "torre-vigia": {
    key: "torre-vigia",
    alt: "Torre de vigia de madeira à beira do penhasco, com guardas na estrada e montanhas nevadas ao fundo",
    width: 1448,
    height: 1086,
    widths: [640, 1024, 1448],
    focus: "60% 45%",
  },
};

export function sceneSrc(key: SceneKey, width: number) {
  return `/art/cenas/${key}-${width}.webp`;
}

export function sceneSrcSet(key: SceneKey, max = Infinity) {
  return scenes[key].widths
    .filter((w) => w <= max)
    .map((w) => `${sceneSrc(key, w)} ${w}w`)
    .join(", ");
}

/** Maior arquivo de até `max` px de largura (para o src de reserva). */
export function sceneFallback(key: SceneKey, max = 1024) {
  const fit = scenes[key].widths.filter((w) => w <= max);
  return sceneSrc(key, fit.length ? fit[fit.length - 1] : scenes[key].widths[0]);
}
