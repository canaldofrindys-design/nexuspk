import type { KitIconName } from "@/components/ui/KitIcon";

/**
 * Eventos fixos do servidor (os que acontecem no jogo, não os cadastrados na agenda do painel).
 * Fonte única: a home e o manual (/manual#pvp) leem daqui. Horários em Brasília.
 */
export type ServerEvent = {
  id: string;
  name: string;
  when: string;
  what: string;
  icon: KitIconName;
  href: string;
  /** Entra na tabela "PvP e guerras" do manual. */
  pvp: boolean;
};

export const serverEvents: ServerEvent[] = [
  {
    id: "bless-castle",
    name: "Bless Castle",
    when: "Domingo, 16:00",
    what: "Guerra de clãs pela posse do castelo",
    icon: "guildasBandeira",
    href: "/manual#pvp",
    pvp: true,
  },
  {
    id: "sod",
    name: "Survival of the Death (SoD)",
    when: "Conforme a agenda",
    what: "Os três melhores clãs ganham as coroas de ouro, prata e bronze, com bônus de HP",
    icon: "trofeu",
    href: "/manual#pvp",
    pvp: true,
  },
  {
    id: "arena-pvp",
    name: "Arena PvP",
    when: "Sempre aberta",
    what: "Atrás do baú de Ricarten. Combate livre entre jogadores",
    icon: "pvp",
    href: "/manual#pvp",
    pvp: true,
  },
  {
    id: "hellsgate",
    name: "Arena Hellsgate",
    when: "Conforme a agenda",
    what: "Arena de desafio do servidor",
    icon: "mapa",
    href: "/manual#pvp",
    pvp: true,
  },
  {
    id: "bosses",
    name: "Bosses e premiação",
    when: "Conforme a agenda",
    what: "Eventos da equipe com bosses e prêmios em coins",
    icon: "boss",
    href: "/eventos",
    pvp: false,
  },
];
