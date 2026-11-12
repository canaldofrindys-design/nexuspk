import Image from "next/image";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { CommunityLinks } from "@/features/site/CommunityLinks";
import { loadSite } from "@/features/site/loadSite";
import { footerColumns } from "./navigation";

/** Redes sociais cadastradas no painel ("Conteúdo do site"). Sem links, nada aparece. */
async function FooterCommunity() {
  await connection();
  const site = await loadSite();
  const links = site?.server.communityLinks ?? [];
  if (links.length === 0) return null;
  return (
    <div className="mt-4">
      <h2 className="font-display text-sm text-gold">Comunidade</h2>
      <CommunityLinks links={links} className="mt-2" />
    </div>
  );
}

export function SiteFooter() {
  return (
    <footer className="mt-8 border-t border-gold/30 bg-abyss/70">
      <div className="mx-auto grid max-w-7xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <div>
          <Image src="/brand/logo-nexuspk.webp" alt="NexusPK" width={400} height={161} className="h-14 w-auto" />
          <p className="mt-3 max-w-xs text-sm text-silver/70">Servidor privado de Priston Tale. Horários em horário de Brasília.</p>
          <Suspense fallback={null}>
            <FooterCommunity />
          </Suspense>
        </div>
        {footerColumns.map((col) => (
          <nav key={col.title} aria-label={col.title}>
            <h2 className="font-display text-sm text-gold">{col.title}</h2>
            <ul className="mt-3 space-y-1.5 text-sm">
              {col.links.map((l) => (
                <li key={l.href}>
                  <Link href={l.href} className="text-silver/80 hover:text-gold">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        ))}
      </div>
      <div className="border-t border-steel/50">
        <p className="mx-auto max-w-7xl px-4 py-4 text-xs text-silver/50">
          Priston Tale é marca de seus respectivos detentores. O NexusPK é um servidor independente, sem afiliação com os detentores da marca.
        </p>
      </div>
    </footer>
  );
}
