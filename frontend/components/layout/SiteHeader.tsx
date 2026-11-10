import Link from "next/link";
import { Crest } from "./Crest";
import { MobileMenu } from "./MobileMenu";
import { mainNavigation } from "./navigation";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-bronze/50 bg-night/90 backdrop-blur">
      <div className="relative mx-auto flex h-16 max-w-7xl items-center justify-between gap-6 px-4">
        <Link href="/" className="flex items-center gap-2" aria-label="NexusPK, página inicial">
          <Crest />
          <span className="font-display text-xl tracking-widest text-gold-bright">NEXUSPK</span>
        </Link>
        <nav aria-label="Principal" className="hidden gap-5 text-sm text-silver lg:flex">
          {mainNavigation.map((item) => (
            <Link key={item.href} href={item.href} className="transition-colors hover:text-gold-bright">
              {item.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-2">
          <Link href="/login" className="hidden px-2 text-sm text-silver hover:text-gold sm:block">
            Entrar
          </Link>
          <Link
            href="/criar-conta"
            className="btn-glow hidden rounded-sm border border-gold-bright/60 bg-gradient-to-b from-gold-bright to-gold px-3 py-1.5 text-sm font-semibold text-night sm:block"
          >
            Criar conta
          </Link>
          <MobileMenu />
        </div>
      </div>
    </header>
  );
}
