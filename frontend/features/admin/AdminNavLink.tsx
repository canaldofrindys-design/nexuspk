"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

/** Link do menu do painel com destaque na página atual. */
export function AdminNavLink({ href, children }: { href: string; children: ReactNode }) {
  const path = usePathname();
  const active = href === "/admin" ? path === "/admin" : path === href || (path.startsWith(href + "/") && !(href === "/admin/jogo" && /^\/admin\/jogo\/(box-send|item-logs)/.test(path)));
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={`block rounded-sm border-l-2 px-2 py-1.5 text-sm transition-colors ${
        active ? "border-gold bg-gold/10 text-gold-bright" : "border-transparent text-silver/75 hover:border-bronze/70 hover:bg-steel/30 hover:text-silver"
      }`}
    >
      {children}
    </Link>
  );
}
