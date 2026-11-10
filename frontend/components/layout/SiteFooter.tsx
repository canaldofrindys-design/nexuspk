import Link from "next/link";
import { Crest } from "./Crest";
import { footerColumns } from "./navigation";

export function SiteFooter() {
  return (
    <footer className="mt-8 border-t border-bronze/40 bg-abyss/60">
      <div className="mx-auto grid max-w-7xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <div>
          <div className="flex items-center gap-2">
            <Crest className="size-8" />
            <span className="font-display tracking-widest text-gold-bright">NEXUSPK</span>
          </div>
          <p className="mt-3 max-w-xs text-sm text-silver/70">Servidor privado de Priston Tale. Horários em horário de Brasília.</p>
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
