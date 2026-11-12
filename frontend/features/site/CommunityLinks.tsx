import { ExternalLink } from "lucide-react";
import { safeHref } from "./links";
import type { LinkItem } from "./types";

/** Links da comunidade (Discord, WhatsApp, Instagram…) cadastrados no painel. Só https. */
export function CommunityLinks({ links, className = "" }: { links: LinkItem[]; className?: string }) {
  const safe = links.map((l) => ({ ...l, href: safeHref(l.url) })).filter((l) => l.href?.startsWith("https://"));
  if (safe.length === 0) return null;
  return (
    <ul className={`flex flex-wrap gap-2 ${className}`}>
      {safe.map((l) => (
        <li key={l.href}>
          <a
            href={l.href!}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 border border-bronze/70 bg-night/60 px-3 py-1.5 text-sm text-silver hover:border-gold hover:text-gold-bright"
          >
            {l.label}
            <ExternalLink className="size-3.5" aria-hidden />
          </a>
        </li>
      ))}
    </ul>
  );
}
