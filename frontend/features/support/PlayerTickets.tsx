import Link from "next/link";
import { formatDateTime } from "@/features/home/format";
import { playerStatusLabels, statusClass, ticketCategories, type TicketSummary } from "./types";

export function PlayerTicketTable({ items }: { items: TicketSummary[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[560px] text-sm">
        <caption className="sr-only">Seus chamados</caption>
        <thead>
          <tr className="text-left text-xs text-silver/60">
            <th scope="col" className="py-1 font-normal">Nº</th>
            <th scope="col" className="py-1 font-normal">Assunto</th>
            <th scope="col" className="py-1 font-normal">Categoria</th>
            <th scope="col" className="py-1 font-normal">Situação</th>
            <th scope="col" className="py-1 text-right font-normal">Atualizado</th>
          </tr>
        </thead>
        <tbody>
          {items.map((t) => (
            <tr key={t.id} className="border-t border-steel/60">
              <td className="tabular py-1.5 text-silver/70">#{t.id}</td>
              <td className="py-1.5">
                <Link href={`/painel/suporte/${t.id}`} className="text-data-name hover:underline">
                  {t.subject}
                </Link>
                {t.playerUnread ? <span className="ml-2 border border-data-ok/70 px-1 text-xs text-data-ok">resposta nova</span> : null}
              </td>
              <td className="py-1.5 text-silver/80">{ticketCategories[t.category] ?? t.category}</td>
              <td className={`py-1.5 ${statusClass[t.status] ?? ""}`}>{playerStatusLabels[t.status] ?? t.status}</td>
              <td className="tabular py-1.5 text-right text-silver/60">{formatDateTime(t.updatedUtc)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
