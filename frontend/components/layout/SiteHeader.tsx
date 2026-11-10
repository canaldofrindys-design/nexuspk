import Link from "next/link";
import { Download, UserRound } from "lucide-react";
import { mainNavigation } from "./navigation";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-gold/20 bg-night/85 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-6 px-4">
        <Link href="/" className="font-display text-xl tracking-widest text-gold">
          NEXUSPK
        </Link>
        <nav aria-label="Principal" className="hidden gap-5 text-sm text-silver lg:flex">
          {mainNavigation.map((item) => (
            <Link key={item.href} href={item.href} className="transition-colors hover:text-gold">
              {item.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-2">
          <Link
            href="/download"
            className="hidden items-center gap-2 rounded-md border border-gold/40 px-3 py-1.5 text-sm text-gold transition hover:bg-gold/10 sm:flex"
          >
            <Download className="size-4" aria-hidden /> Download
          </Link>
          <Link
            href="/painel"
            className="flex items-center gap-2 rounded-md bg-arcane px-3 py-1.5 text-sm font-medium text-night transition hover:brightness-110"
          >
            <UserRound className="size-4" aria-hidden /> Painel
          </Link>
        </div>
      </div>
    </header>
  );
}
