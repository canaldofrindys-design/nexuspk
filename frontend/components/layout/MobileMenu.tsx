"use client";

import Link from "next/link";
import { Menu, X } from "lucide-react";
import { useState, type ReactNode } from "react";
import { mainNavigation } from "./navigation";

/** Menu do celular. `account` traz os links da conta (Entrar/Criar conta ou Painel), renderizados no servidor. */
export function MobileMenu({ account }: { account?: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="lg:hidden">
      <button
        type="button"
        aria-expanded={open}
        aria-controls="mobile-nav"
        aria-label={open ? "Fechar menu" : "Abrir menu"}
        onClick={() => setOpen((v) => !v)}
        className="grid size-9 place-items-center border border-bronze text-gold"
      >
        {open ? <X className="size-5" /> : <Menu className="size-5" />}
      </button>
      {open && (
        <nav
          id="mobile-nav"
          aria-label="Principal"
          className="game-window absolute inset-x-0 top-16 z-50 grid gap-1 p-3"
          onClick={(e) => {
            if ((e.target as HTMLElement).closest("a")) setOpen(false);
          }}
        >
          {mainNavigation.map((item) => (
            <Link key={item.href} href={item.href} className="px-2 py-2 text-silver hover:text-gold">
              {item.label}
            </Link>
          ))}
          {account ? <div className="mt-2 flex flex-col gap-1 border-t border-bronze/50 pt-2">{account}</div> : null}
        </nav>
      )}
    </div>
  );
}
