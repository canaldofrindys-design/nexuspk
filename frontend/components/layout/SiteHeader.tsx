import Image from "next/image";
import Link from "next/link";
import { Suspense } from "react";
import { GuestLinks, HeaderAccount, MobileAccount, MobileGuestLinks } from "./HeaderAccount";
import { MobileMenu } from "./MobileMenu";
import { mainNavigation } from "./navigation";

export function SiteHeader() {
  return (
    <header
      className="site-header sticky top-0 z-40 border-b border-gold/30 bg-night/70 backdrop-blur-md"
      style={{ viewTransitionName: "site-header" }}
    >
      <div className="relative mx-auto flex h-[68px] max-w-7xl items-center justify-between gap-6 px-4">
        <Link href="/" className="flex shrink-0 items-center" aria-label="NexusPK, página inicial">
          <Image
            src="/brand/logo-nexuspk.webp"
            alt="NexusPK"
            width={400}
            height={161}
            priority
            className="h-11 w-auto drop-shadow-[0_0_12px_rgb(95_178_255/0.35)]"
          />
        </Link>
        <nav aria-label="Principal" className="hidden gap-5 text-sm text-silver lg:flex">
          {mainNavigation.map((item) => (
            <Link key={item.href} href={item.href} className="py-1 tracking-wide transition-colors hover:text-gold-bright">
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
      <div aria-hidden className="scroll-progress" />
    </header>
  );
}
