import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatDateTime } from "./format";
import type { HomeEventItem } from "./types";

export function AgendaWindow({ active, upcoming }: { active: HomeEventItem[]; upcoming: HomeEventItem[] }) {
  const rows = [...active.map((e) => ({ ...e, live: true })), ...upcoming.map((e) => ({ ...e, live: false }))];
  return (
    <GameWindow title="Agenda de eventos">
      {rows.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum evento ativo ou agendado. Os horários seguem o horário de Brasília.</p>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-silver/60">
              <th scope="col" className="py-1 font-normal">Início</th>
              <th scope="col" className="py-1 font-normal">Evento</th>
              <th scope="col" className="py-1 text-right font-normal">Situação</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((e) => (
              <tr key={e.slug} className="border-t border-steel/60">
                <td className="tabular py-1.5 text-silver/80">{formatDateTime(e.startsAtUtc)}</td>
                <td className="py-1.5">
                  <Link href={`/eventos/${e.slug}`} className="hover:text-gold hover:underline">
                    {e.name}
                  </Link>
                </td>
                <td className={`py-1.5 text-right ${e.live ? "text-data-ok" : "text-data-info"}`}>{e.live ? "Agora" : "Agendado"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </GameWindow>
  );
}
