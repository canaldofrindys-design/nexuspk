import { formatDateTime } from "@/features/home/format";
import { formatBytes, type ThreadMessage } from "./types";

/** Conversa do chamado. O texto aparece como texto puro; anexos abrem pela rota protegida de cada painel. */
export function Thread({ messages, attachmentHref, viewer }: { messages: ThreadMessage[]; attachmentHref: (attachmentId: string) => string; viewer: "jogador" | "equipe" }) {
  return (
    <ol className="flex flex-col gap-3">
      {messages.map((m) => {
        const mine = m.authorType === viewer;
        const who = m.authorType === "jogador" ? (viewer === "jogador" ? "Você" : "Jogador") : (m.staffName ?? "Equipe");
        return (
          <li
            key={m.id}
            className={`border px-3 py-2 ${m.internal ? "border-dashed border-data-info/60 bg-data-info/5" : mine ? "border-bronze/50 bg-night/60" : "border-gold/40 bg-gold/5"}`}
          >
            <p className="mb-1 flex flex-wrap items-baseline justify-between gap-2 text-xs">
              <span className={m.authorType === "equipe" ? "font-semibold text-gold-bright" : "font-semibold text-data-name"}>
                {who}
                {m.internal ? <span className="ml-2 font-normal text-data-info">nota interna</span> : null}
              </span>
              <time dateTime={m.createdUtc} className="tabular text-silver/60">
                {formatDateTime(m.createdUtc)}
              </time>
            </p>
            <p className="whitespace-pre-wrap break-words text-sm text-silver">{m.body}</p>
            {m.attachments.length > 0 ? (
              <ul className="mt-2 flex flex-wrap gap-2">
                {m.attachments.map((a) => (
                  <li key={a.id}>
                    <a href={attachmentHref(a.id)} target="_blank" rel="noopener" className="block border border-bronze/60 p-1 hover:border-gold">
                      {/* eslint-disable-next-line @next/next/no-img-element -- imagem protegida servida por rota própria */}
                      <img src={attachmentHref(a.id)} alt={`Anexo ${a.fileName}`} width={160} height={120} loading="lazy" className="h-[120px] w-[160px] object-contain" />
                      <span className="block max-w-[160px] truncate text-xs text-silver/70">
                        {a.fileName} ({formatBytes(a.sizeBytes)})
                      </span>
                    </a>
                  </li>
                ))}
              </ul>
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}
