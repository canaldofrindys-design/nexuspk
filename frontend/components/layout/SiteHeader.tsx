import Link from "next/link";
import { Suspense } from "react";
import { GuestLinks, HeaderAccount, MobileAccount, MobileGuestLinks } from "./HeaderAccount";
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
          <Suspense fallback={<GuestLinks />}>
            <HeaderAccount />
          </Suspense>
          <MobileMenu
            account={
              <Suspense fallback={<MobileGuestLinks />}>
                <MobileAccount />
              </Suspense>
            }
          />
        </div>
      </div>
    </header>
  );
}
