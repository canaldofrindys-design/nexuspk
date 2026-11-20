import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { staffLogoutAction } from "@/features/admin/actions";
import { AdminNavLink } from "@/features/admin/AdminNavLink";
import { getStaff } from "@/features/admin/session";
import type { Capability } from "@/features/admin/types";

type NavItem = { href: string; label: string; needs: Capability[] };

const groups: { title: string; items: NavItem[] }[] = [
  { title: "Geral", items: [{ href: "/admin", label: "Resumo", needs: ["ViewDashboard"] }] },
  {
    title: "Jogadores",
    items: [
      { href: "/admin/contas", label: "Contas e banimentos", needs: ["ViewAccounts"] },
      { href: "/admin/jogo/box-send", label: "Enviar Box Donate", needs: ["ManageEconomy"] },
      { href: "/admin/jogo/item-logs", label: "Logs de itens", needs: ["ViewGameLogs"] },
    ],
  },
  {
    title: "Jogo",
    items: [
      { href: "/admin/jogo", label: "Lojas, drops e itens", needs: ["ManageGameData", "ViewGameLogs", "ConfigureGameSources"] },
      { href: "/admin/rankings", label: "Reset de rankings", needs: ["ResetRankings"] },
      { href: "/admin/servidor", label: "Ligar / reiniciar", needs: ["ControlServer"] },
    ],
  },
  {
    title: "Site",
    items: [
      { href: "/admin/noticias", label: "Notícias", needs: ["ManageContent"] },
      { href: "/admin/eventos", label: "Eventos", needs: ["ManageContent"] },
      { href: "/admin/banners", label: "Banners", needs: ["ManageContent"] },
      { href: "/admin/site", label: "Conteúdo do site", needs: ["ManageSite"] },
      { href: "/admin/avisos", label: "Avisos de status", needs: ["ManageContent"] },
    ],
  },
  {
    title: "Equipe",
    items: [
      { href: "/admin/chamados", label: "Chamados", needs: ["ManageSupport"] },
      { href: "/admin/equipe", label: "Equipe", needs: ["ManageStaff"] },
      { href: "/admin/auditoria", label: "Auditoria", needs: ["ViewAudit"] },
    ],
  },
];

async function StaffNav() {
  await connection();
  const me = await getStaff();
  if (!me) return null;
  const visible = groups
    .map((g) => ({ ...g, items: g.items.filter((i) => i.needs.some((n) => me.capabilities.includes(n))) }))
    .filter((g) => g.items.length > 0);
  return (
    <div className="flex flex-col gap-5">
      <div className="rounded-md border border-steel/70 bg-night/60 p-3 text-sm">
        <p className="text-silver">{me.displayName}</p>
        <p className="text-xs text-gold/80">{me.roleLabel}</p>
        <div className="mt-2 flex gap-3 text-xs">
          <Link href="/admin/senha" className="text-arcane hover:underline">
            Trocar senha
          </Link>
          <form action={staffLogoutAction}>
            <button type="submit" className="text-arcane hover:underline">
              Sair
            </button>
          </form>
        </div>
      </div>
      <nav aria-label="Painel" className="flex flex-col gap-4">
        {visible.map((g) => (
          <div key={g.title}>
            <p className="mb-1.5 px-2 text-[0.68rem] uppercase tracking-[0.18em] text-silver/45">{g.title}</p>
            <ul className="flex flex-col gap-0.5">
              {g.items.map((i) => (
                <li key={i.href}>
                  <AdminNavLink href={i.href}>{i.label}</AdminNavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
    </div>
  );
}

export default function PanelLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-[90rem] px-4 py-8">
      <p className="mb-5 font-display text-lg text-gold-bright">Painel da equipe</p>
      <div className="grid gap-6 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <aside className="lg:sticky lg:top-24 lg:self-start">
          <Suspense fallback={null}>
            <StaffNav />
          </Suspense>
        </aside>
        <div className="min-w-0">{children}</div>
      </div>
    </div>
  );
}
