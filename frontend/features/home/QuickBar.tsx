import Link from "next/link";
import { CalendarDays, LifeBuoy, Swords, Trophy } from "lucide-react";

const shortcuts = [
  { href: "/ranking", label: "Ranking", Icon: Trophy },
  { href: "/eventos", label: "Eventos", Icon: CalendarDays },
  { href: "/classes", label: "Classes", Icon: Swords },
  { href: "/suporte", label: "Suporte", Icon: LifeBuoy },
] as const;

/**
 * Atalhos do celular no formato de barra de habilidades do jogo, logo abaixo da abertura.
 * Some no PC (lá o menu do cabeçalho já mostra tudo).
 */
export function QuickBar() {
  return (
    <nav aria-label="Atalhos" className="lg:hidden">
      <ul className="grid grid-cols-4 gap-2">
        {shortcuts.map(({ href, label, Icon }, i) => (
          <li key={href} data-reveal="zoom" style={{ "--reveal-delay": `${i * 70}ms` } as React.CSSProperties}>
            <Link
              href={href}
              className="quick-slot group flex flex-col items-center gap-1.5 px-1 pt-2.5 pb-2 text-[11px] tracking-wide text-silver/85 active:text-gold-bright"
            >
              <span className="quick-slot__icon grid size-10 place-items-center text-gold">
                <Icon className="size-5" strokeWidth={1.6} aria-hidden />
              </span>
              {label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
