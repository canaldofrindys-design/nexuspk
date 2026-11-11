import Link from "next/link";
import { formatDateTime } from "@/features/home/format";
import { eventLabel, eventStates } from "./labels";
import type { EventSummary } from "./types";

/** Tabela de eventos: início, nome, categoria e situação. */
export function EventList({ events, empty }: { events: EventSummary[]; empty: string }) {
  if (events.length === 0) return <p className="text-sm text-silver/70">{empty}</p>;
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-xs text-silver/60">
          <th scope="col" className="py-1 font-normal">Início</th>
          <th scope="col" className="py-1 font-normal">Evento</th>
          <th scope="col" className="hidden px-2 py-1 font-normal sm:table-cell">Tipo</th>
          <th scope="col" className="py-1 text-right font-normal">Situação</th>
        </tr>
      </thead>
      <tbody>
        {events.map((e) => (
          <tr key={e.slug} className="border-t border-steel/60 align-baseline">
            <td className="tabular whitespace-nowrap py-1.5 pr-3 text-silver/80">{formatDateTime(e.startsUtc)}</td>
            <td className="py-1.5">
              <Link href={`/eventos/${e.slug}`} className="text-data-name hover:text-gold-bright hover:underline">
                {e.name}
              </Link>
              <span className="block text-xs text-silver/60 sm:hidden">{eventLabel(e.category)}</span>
            </td>
            <td className="hidden px-2 py-1.5 text-silver/80 sm:table-cell">{eventLabel(e.category)}</td>
            <td className={`whitespace-nowrap py-1.5 pl-2 text-right ${eventStates[e.state].tone}`}>{eventStates[e.state].label}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
