import Image from "next/image";
import Link from "next/link";
import { Suspense } from "react";
import { kitArt } from "@/components/ui/ArtLink";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";
import { GuestLinks, HeaderAccount, MobileAccount, MobileGuestLinks } from "./HeaderAccount";
import { MobileMenu } from "./MobileMenu";
import { NavLinks, NavList } from "./NavLinks";

/**
 * Cabeçalho da prancha "home" do kit: arte com o logo e os heróis de ponta a ponta e, logo abaixo,
 * o menu em moldura dourada com cristais nas pontas. O menu fica preso no topo ao rolar.
 * No celular a arte é recortada no centro (logo) e o menu vira o botão de abrir.
 */
const quick = [
  { href: "/", label: "Início", icon: "inicio" },
  { href: "/ranking", label: "Ranking", icon: "trofeu" },
  { href: "/eventos", label: "Eventos", icon: "eventos" },
  { href: "/download", label: "Download", icon: "download" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName }[];

export function SiteHeader() {
  const art = kitArt.banners.cabecalho;
  return (
    <>
      <div className="nx-banner relative">
        <Link href="/" aria-label="NexusPK, página inicial" className="block">
          <Image
            src={art.src}
            alt=""
            width={art.width}
            height={art.height}
            priority
            unoptimized
            className="mx-auto h-[118px] w-full object-cover object-center sm:h-[150px] lg:h-auto lg:max-h-[190px]"
          />
        </Link>
      </div>
      <header className="site-header sticky top-0 z-40 -mt-5 overflow-x-clip px-4 pt-1 pb-1" style={{ viewTransitionName: "site-header" }}>
        <div className="nx-navbar relative mx-auto flex h-[54px] max-w-7xl items-stretch px-3 lg:px-6">
          <span aria-hidden className="nx-gem -left-3" />
          <span aria-hidden className="nx-gem -right-3" />
          <nav aria-label="Atalhos" className="flex flex-1 items-stretch lg:hidden">
            {quick.map((q) => (
              <Link key={q.href} href={q.href} className="nx-nav-item flex flex-1 flex-col items-center justify-center gap-0.5 text-[10px] font-semibold tracking-wide text-silver uppercase">
                <KitIcon name={q.icon} size={26} />
                {q.label}
              </Link>
            ))}
          </nav>
          <nav aria-label="Principal" className="hidden flex-1 lg:flex">
            <Suspense fallback={<NavList />}>
              <NavLinks />
            </Suspense>
          </nav>
          <div className="flex items-center justify-end gap-2 pl-2 lg:pl-3">
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
    </>
  );
}
