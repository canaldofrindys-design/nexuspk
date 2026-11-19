import Link from "next/link";
import { KitIcon, type KitIconName } from "@/components/ui/KitIcon";

const shortcuts = [
  { href: "/ranking", label: "Ranking", icon: "trofeu" },
  { href: "/eventos", label: "Eventos", icon: "eventos" },
  { href: "/classes", label: "Classes", icon: "classes" },
  { href: "/download", label: "Download", icon: "download" },
  { href: "/suporte", label: "Suporte", icon: "guia" },
] as const satisfies readonly { href: string; label: string; icon: KitIconName }[];

/**
 * Atalhos do celular no formato de barra de habilidades do jogo, logo abaixo da abertura.
 * Some no PC (lá o menu do cabeçalho já mostra tudo).
 */
export function QuickBar() {
  return (
    <nav aria-label="Atalhos" className="lg:hidden">
      <ul className="grid grid-cols-5 gap-1.5 sm:gap-2">
        {shortcuts.map(({ href, label, icon }, i) => (
          <li key={href} data-reveal="zoom" style={{ "--reveal-delay": `${i * 70}ms` } as React.CSSProperties}>
            <Link
              href={href}
              className="quick-slot group flex flex-col items-center gap-1.5 px-1 pt-2.5 pb-2 text-[11px] tracking-wide text-silver/85 active:text-gold-bright"
            >
              <span className="quick-slot__icon grid size-10 place-items-center text-gold">
                <KitIcon name={icon} size={36} />
              </span>
              {label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
