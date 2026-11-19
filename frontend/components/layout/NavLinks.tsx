"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { KitIcon } from "@/components/ui/KitIcon";
import { isActive, mainNavigation } from "./navigation";

/** Destaca a página atual (precisa do endereço, por isso fica dentro de um Suspense no cabeçalho). */
export function NavLinks() {
  return <NavList path={usePathname()} />;
}

/** Itens do menu do PC, divididos por filetes dourados, com o ícone do kit. Sem `path`, nenhum fica destacado. */
export function NavList({ path }: { path?: string }) {
  return (
    <ul className="flex flex-1 items-stretch">
      {mainNavigation
        .filter((i) => !("extra" in i && i.extra))
        .map((item) => {
          const active = path != null && isActive(item, path);
          return (
            <li key={item.href} className="nx-nav-item flex flex-1">
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className="group flex flex-1 items-center justify-center gap-2 px-2 text-[13px] font-semibold tracking-wide whitespace-nowrap text-silver uppercase transition-colors xl:text-sm"
              >
                <KitIcon name={item.icon} size={26} className="transition-transform group-hover:scale-110" />
                {item.label}
              </Link>
            </li>
          );
        })}
    </ul>
  );
}
