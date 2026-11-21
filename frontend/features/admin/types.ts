export type Capability =
  | "ViewDashboard"
  | "ManageContent"
  | "ViewAudit"
  | "ManageStaff"
  | "ManageSupport"
  | "ManageSite"
  | "ViewAccounts"
  | "ManageAccounts"
  | "ManageEconomy"
  | "ManageGameData"
  | "ViewGameLogs"
  | "ControlServer"
  | "ResetRankings"
  | "ConfigureGameSources";

export type StaffMe = {
  id: number;
  username: string;
  displayName: string;
  role: string;
  roleLabel: string;
  mustChangePassword: boolean;
  sessionExpiresUtc: string;
  capabilities: Capability[];
};

export type AdminList<T> = { items: T[]; total: number; page: number; pageSize: number };

export type NewsRecord = {
  id: number;
  slug: string;
  category: string;
  title: string;
  summary: string;
  body: string;
  imageUrl: string | null;
  pinned: boolean;
  status: "draft" | "published" | "archived";
  publishedUtc: string | null;
  createdUtc: string;
  updatedUtc: string;
  createdBy: string | null;
  updatedBy: string | null;
};

export type EventRecord = {
  id: number;
  slug: string;
  category: string;
  name: string;
  summary: string;
  description: string;
  rules: string | null;
  prizes: string | null;
  location: string | null;
  imageUrl: string | null;
  startsUtc: string;
  endsUtc: string | null;
  status: "draft" | "published" | "cancelled";
  createdUtc: string;
  updatedUtc: string;
  createdBy: string | null;
  updatedBy: string | null;
};

export type StaffItem = {
  id: number;
  username: string;
  displayName: string;
  role: string;
  active: boolean;
  mustChangePassword: boolean;
  createdUtc: string;
  lastLoginUtc: string | null;
};

export type AuditEntry = {
  id: number;
  occurredUtc: string;
  actorType: string;
  actorId: string | null;
  action: string;
  targetType: string | null;
  targetId: string | null;
  ipAddress: string | null;
  details: string | null;
};

export type Dashboard = {
  game: { accounts: number; characters: number } | null;
  portal: {
    registrations24h: number;
    registrations7d: number;
    registrations30d: number;
    accountsWithConfirmedEmail: number;
    newsPublished: number;
    newsDrafts: number;
    eventsLive: number;
    eventsUpcoming: number;
    ticketsActive: number;
    ticketsWaitingStaff: number;
    tickets24h: number;
  } | null;
  server: { availability: string; checkedAtUtc: string | null; playersOnline: number | null };
  generatedUtc: string;
  integration: { name: string; status: string; ms: number }[];
};

export const roleLabels: Record<string, string> = {
  superadmin: "Superadministrador",
  admin: "Administrador",
  moderador: "Moderador",
  suporte: "Suporte",
};

export const newsStatusLabels = { draft: "Rascunho", published: "Publicada", archived: "Arquivada" } as const;
export const eventStatusLabels = { draft: "Rascunho", published: "Publicado", cancelled: "Cancelado" } as const;

/** Resposta das ações do painel do jogo. */
export type GameOk<T = unknown> = { message: string | null; data: T };

export type AccountHit = { login: string; matchedCharacter: string | null; blocked: boolean; deleted: boolean; registDay: string | null };

export type GameBan = {
  id: number;
  gameLogin: string;
  reason: string;
  bannedBy: string;
  bannedUtc: string;
  expiresUtc: string | null;
  liftedUtc: string | null;
  liftedBy: string | null;
  liftReason: string | null;
};

export type AccountVip = {
  playerName: string | null;
  plan: number | null;
  planLabel: string;
  startLocal: string | null;
  endLocal: string | null;
  endsUtc: string | null;
  active: boolean;
};

export type DiagnosticCheck = { key: string; level: "ok" | "info" | "aviso" | "problema"; title: string; detail: string };

export type AccountView = {
  login: string;
  registeredUtc: string | null;
  lastDayUtc: string | null;
  blocked: boolean;
  deleted: boolean;
  coins: number | null;
  gameMasterLevel: number | null;
  characters: { name: string; className: string; level: number | null; clan: string | null }[];
  vips: AccountVip[];
  pvp: { nick: string | null; battlePoints: number | null; weeklyBattlePoints: number; kills: number; deaths: number }[];
  portal: { gameLogin: string; email: string | null; emailConfirmedUtc: string | null; createdUtc: string; lastLoginUtc: string | null } | null;
  activePortalSessions: number;
  bans: GameBan[];
  activity: AuditEntry[];
  diagnostics: DiagnosticCheck[];
  vipPlans: Record<string, string>;
};

export type ServerProcess = {
  key: string;
  name: string;
  state: "stopped" | "running" | "runningOtherOwner" | "missing";
  instances: number;
  startedUtc: string | null;
  note: string | null;
};

export type ServerPage = { enabled: boolean; processes: ServerProcess[]; confirmWords: { start: string; stop: string; restart: string } };

export type RankingBackup = { id: number; kind: string; createdBy: string; createdUtc: string; rows: number; restoredUtc: string | null; restoredBy: string | null };

export type RankingsPage = { enabled: boolean; confirmWord: string; kinds: { key: string; label: string; description: string }[]; backups: RankingBackup[] };

export type GameFunctionSummary = {
  key: string;
  label: string;
  description: string;
  mode: "edit" | "insert" | "readonly";
  configured: boolean;
  source: string | null;
  canWrite: boolean;
  canConfigure: boolean;
};

export type GameColumnView = {
  name: string;
  type: string;
  maxChars: number;
  nullable: boolean;
  identity: boolean;
  computed: boolean;
  hasDefault: boolean;
  supported: boolean;
  insertable: boolean;
  isKey: boolean;
  isEditable: boolean;
  isSearch: boolean;
  isAccount: boolean;
};

export type GameSourceBinding = {
  functionKey: string;
  database: string;
  table: string;
  keyColumns: string[];
  editableColumns: string[];
  searchColumns: string[];
  accountColumn: string | null;
  allowInsert: boolean;
  allowDelete: boolean;
  newestFirst: boolean;
  updatedBy: string;
  updatedUtc: string;
};

export type BrowseView = {
  function: GameFunctionSummary;
  binding: GameSourceBinding;
  columns: GameColumnView[];
  rows: Record<string, string | null>[];
  page: number;
  hasMore: boolean;
  canEdit: boolean;
  canInsert: boolean;
  canDelete: boolean;
};

export type SourceConfigPage = {
  function: { key: string; label: string; description: string; mode: "edit" | "insert" | "readonly"; suggestedDatabases: string[] };
  binding: GameSourceBinding | null;
  databases: string[] | null;
  message: string | null;
};

export type TableSuggestion = { name: string; rows: number; suggested: boolean };

/** Item do servidor: código do arquivo ("WA161"), nome (pasta OpenItem) e se há imagem (copiada do cliente). */
export type CatalogItem = { id: string; name: string | null; image: boolean };
export type ItemCatalog = { images: number; items: CatalogItem[] };

/** Monstro lido dos arquivos do servidor (Monster\*.inf), com as linhas de drop. */
export type MonsterDropView = { chance: string; gold: string | null; items: CatalogItem[] };
export type MonsterView = { name: string; file: string; hp: number | null; drops: MonsterDropView[] };
export type MonsterList = { available: boolean; monsters: MonsterView[] };

/** Loja de NPC lida dos arquivos do servidor (NPC\*.npc): armas, armaduras e o resto à venda. */
export type NpcShopView = { name: string; file: string; weapons: CatalogItem[]; armors: CatalogItem[]; misc: CatalogItem[] };
export type NpcShopList = { available: boolean; shops: NpcShopView[] };
