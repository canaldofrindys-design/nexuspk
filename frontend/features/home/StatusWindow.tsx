import { GameWindow } from "@/components/ui/GameWindow";
import { formatTime } from "./format";
import type { HomeSnapshot } from "./types";

const label = {
  online: { text: "Online", className: "text-data-ok" },
  offline: { text: "Offline", className: "text-data-bad" },
  unknown: { text: "Sem verificação", className: "text-silver/70" },
} as const;

export function StatusWindow({ status }: { status: HomeSnapshot["status"] | null }) {
  const state = label[status?.availability ?? "unknown"];
  return (
    <GameWindow title="Servidor">
      <dl className="grid grid-cols-[1fr_auto] gap-y-1.5 text-sm">
        <dt>Estado</dt>
        <dd className={`text-right font-semibold ${state.className}`}>{status ? state.text : "API indisponível"}</dd>
        <dt>Jogadores online</dt>
        <dd className="tabular text-right text-data-info" title="O servidor ainda não publica a contagem de jogadores">
          {status?.playersOnline ?? "—"}
        </dd>
        <dt>Verificado às</dt>
        <dd className="tabular text-right text-silver/80">{status?.checkedAtUtc ? formatTime(status.checkedAtUtc) : "—"}</dd>
      </dl>
    </GameWindow>
  );
}
