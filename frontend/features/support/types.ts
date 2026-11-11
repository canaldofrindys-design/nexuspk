export const ticketCategories: Record<string, string> = {
  conta: "Conta e acesso",
  personagem: "Personagem e itens",
  bug: "Erro no jogo",
  denuncia: "Denúncia",
  evento: "Eventos",
  loja: "Loja e coins",
  outro: "Outro assunto",
};

export const ticketStatuses: Record<string, string> = {
  aberto: "Aberto",
  em_atendimento: "Em atendimento",
  aguardando: "Aguardando jogador",
  resolvido: "Resolvido",
  fechado: "Fechado",
};

/** Para o jogador, "aguardando" quer dizer que a resposta é dele. */
export const playerStatusLabels: Record<string, string> = {
  ...ticketStatuses,
  aguardando: "Respondido pela equipe",
};

export const ticketPriorities: Record<string, string> = { baixa: "Baixa", normal: "Normal", alta: "Alta" };

export const statusClass: Record<string, string> = {
  aberto: "text-data-name",
  em_atendimento: "text-data-info",
  aguardando: "text-data-ok",
  resolvido: "text-silver/70",
  fechado: "text-silver/50",
};

export const MAX_FILES = 3;
export const MAX_FILE_BYTES = 3 * 1024 * 1024;
export const ACCEPT = "image/png,image/jpeg,image/bmp";

export type Attachment = { id: string; messageId: number; fileName: string; contentType: string; sizeBytes: number };

export type TicketSummary = {
  id: number;
  gameLogin: string;
  category: string;
  subject: string;
  status: string;
  priority: string;
  assignedStaffId: number | null;
  assignedName: string | null;
  createdUtc: string;
  updatedUtc: string;
  lastAuthor: "jogador" | "equipe";
  playerUnread: boolean;
  closedUtc: string | null;
  closedBy: string | null;
};

export type TicketList = { items: TicketSummary[]; total: number; page: number; pageSize: number };

export type ThreadMessage = {
  id: number;
  authorType: "jogador" | "equipe";
  staffName: string | null;
  body: string;
  internal?: boolean;
  createdUtc: string;
  attachments: Attachment[];
};

export type PlayerTicket = {
  ticket: { id: number; category: string; subject: string; status: string; createdUtc: string; updatedUtc: string; closedUtc: string | null; closedByPlayer: boolean };
  messages: ThreadMessage[];
};

export type StaffTicket = { ticket: TicketSummary; messages: ThreadMessage[] };

export type SupportAgent = { id: number; username: string; displayName: string };

export type ServerNotice = {
  id: number;
  kind: "aviso" | "manutencao";
  title: string;
  body: string;
  startsUtc: string;
  endsUtc: string | null;
  status: "draft" | "published" | "cancelled";
  createdUtc: string;
  updatedUtc: string;
  createdBy: string | null;
  updatedBy: string | null;
};

export const noticeKinds: Record<string, string> = { aviso: "Aviso", manutencao: "Manutenção" };
export const noticeStatuses: Record<string, string> = { draft: "Rascunho", published: "Publicado", cancelled: "Cancelado" };

export type StatusReport = {
  availability: "unknown" | "online" | "offline" | "partial";
  checkedUtc: string | null;
  targets: {
    key: string;
    name: string;
    online: boolean | null;
    latencyMs: number | null;
    uptime24h: number | null;
    uptime7d: number | null;
    last24h: { hourUtc: string; online: number | null }[];
  }[];
  historySinceUtc: string | null;
  playersOnline: number | null;
  maintenanceNow: boolean;
  notices: { id: number; kind: "aviso" | "manutencao"; title: string; body: string; startsUtc: string; endsUtc: string | null; active: boolean }[];
  generatedUtc: string;
};

export function formatBytes(n: number) {
  return n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1).replace(".", ",")} MB` : `${Math.max(1, Math.round(n / 1024))} KB`;
}
