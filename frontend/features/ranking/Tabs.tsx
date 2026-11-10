import Link from "next/link";

export type Tab = { href: string; label: string; active: boolean };

/** Abas como links: funcionam sem JavaScript e cada aba tem endereço próprio. */
export function Tabs({ tabs, label }: { tabs: Tab[]; label: string }) {
  return (
    <nav aria-label={label} className="flex flex-wrap gap-1">
      {tabs.map((t) => (
        <Link
          key={t.href}
          href={t.href}
          aria-current={t.active ? "page" : undefined}
          className={`border px-3 py-1 text-sm ${
            t.active ? "border-gold bg-gold/15 text-gold-bright" : "border-bronze/60 text-silver/80 hover:border-gold hover:text-gold"
          }`}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );
}
