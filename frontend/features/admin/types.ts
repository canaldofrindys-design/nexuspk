export type Capability = "ViewDashboard" | "ManageContent" | "ViewAudit" | "ManageStaff";

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
