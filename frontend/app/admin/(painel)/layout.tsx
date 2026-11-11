import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { staffLogoutAction } from "@/features/admin/actions";
import { getStaff } from "@/features/admin/session";
import type { Capability } from "@/features/admin/types";

const links: { href: string; label: string; needs: Capability }[] = [
  { href: "/admin", label: "Resumo", needs: "ViewDashboard" },
  { href: "/admin/noticias", label: "Notícias", needs: "ManageContent" },
  { href: "/admin/eventos", label: "Eventos", needs: "ManageContent" },
  { href: "/admin/chamados", label: "Chamados", needs: "ManageSupport" },
  { href: "/admin/avisos", label: "Avisos de status", needs: "ManageContent" },
  { href: "/admin/equipe", label: "Equipe", needs: "ManageStaff" },
  { href: "/admin/auditoria", label: "Auditoria", needs: "ViewAudit" },
];

async function StaffNav() {
  await connection();
  const me = await getStaff();
  if (!me) return null;
  return (
    <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <nav aria-label="Painel" className="flex flex-wrap gap-1">
        {links
          .filter((l) => me.capabilities.includes(l.needs))
          .map((l) => (
            <Link key={l.href} href={l.href} className="border border-bronze/60 px-3 py-1 text-sm text-silver/80 hover:border-gold hover:text-gold">
              {l.label}
            </Link>
          ))}
      </nav>
      <div className="flex flex-wrap items-center gap-3 text-sm text-silver/70">
        <span>
          {me.displayName} <span className="text-silver/50">({me.roleLabel})</span>
        </span>
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
  );
}

export default function PanelLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-8">
      <div className="mb-6 border-b border-steel/60 pb-4">
        <p className="mb-3 font-display text-lg text-gold-bright">Painel da equipe</p>
        <Suspense fallback={null}>
          <StaffNav />
        </Suspense>
      </div>
      {children}
    </div>
  );
}
