import Link from "next/link";
import { localDay } from "./dates";
import { eventStates } from "./labels";
import type { EventSummary } from "./types";

const weekdays = ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"];

function iso(y: number, m: number, d: number) {
  return `${y}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

/** Dias (Brasília) em que o evento acontece. Evento sem fim ocupa só o dia do início. */
function daysOf(e: EventSummary): string[] {
  const first = localDay(e.startsUtc);
  if (!e.endsUtc) return [first];
  // O fim é exclusivo: um evento que termina à meia-noite não ocupa o dia seguinte.
  const last = localDay(new Date(new Date(e.endsUtc).getTime() - 1000));
  const out: string[] = [];
  const d = new Date(`${first}T12:00:00Z`);
  const stop = new Date(`${last}T12:00:00Z`);
  while (d <= stop && out.length < 62) {
    out.push(d.toISOString().slice(0, 10));
    d.setUTCDate(d.getUTCDate() + 1);
  }
  return out;
}

/** Calendário do mês (segunda a domingo, horário de Brasília). */
export function Calendar({ year, month, events, today }: { year: number; month: number; events: EventSummary[]; today: string }) {
  const byDay = new Map<string, EventSummary[]>();
  for (const e of events) for (const day of daysOf(e)) byDay.set(day, [...(byDay.get(day) ?? []), e]);

  const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const offset = (new Date(Date.UTC(year, month - 1, 1)).getUTCDay() + 6) % 7;
  const cells: (number | null)[] = [...Array<null>(offset).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];
  while (cells.length % 7 !== 0) cells.push(null);

  return (
    <table className="w-full table-fixed border-collapse text-sm">
      <thead>
        <tr>
          {weekdays.map((w) => (
            <th key={w} scope="col" className="py-1 text-center text-xs font-normal text-silver/60">
              {w}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {Array.from({ length: cells.length / 7 }, (_, row) => (
          <tr key={row}>
            {cells.slice(row * 7, row * 7 + 7).map((day, i) => {
              if (day === null) return <td key={i} className="border border-steel/40 bg-night/40" />;
              const key = iso(year, month, day);
              const list = byDay.get(key) ?? [];
              const isToday = key === today;
              return (
                <td key={i} className={`h-14 border border-steel/60 p-1 align-top md:h-24 ${isToday ? "bg-gold/10" : ""}`}>
                  <span className={`tabular text-xs ${isToday ? "font-semibold text-gold-bright" : "text-silver/70"}`}>{day}</span>
                  {list.length > 0 ? (
                    <>
                      <span className="tabular block text-[11px] text-data-info md:hidden" aria-label={`${list.length} evento(s)`}>
                        {list.length} ev.
                      </span>
                      <ul className="hidden md:block">
                        {list.slice(0, 3).map((e) => (
                          <li key={e.slug} className="truncate text-xs leading-5">
                            <Link href={`/eventos/${e.slug}`} className={`hover:underline ${eventStates[e.state].tone}`}>
                              {e.name}
                            </Link>
                          </li>
                        ))}
                        {list.length > 3 ? <li className="text-xs text-silver/60">+{list.length - 3}</li> : null}
                      </ul>
                    </>
                  ) : null}
                </td>
              );
            })}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
