import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatTime } from "./format";
import type { HomeSnapshot } from "./types";

const label = {
  online: { text: "Online", className: "text-data-ok" },
  offline: { text: "Offline", className: "text-data-bad" },
  partial: { text: "Parcial", className: "text-data-name" },
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
        <dd className="tabular text-right text-data-info" title="Contas conectadas agora, segundo o servidor do jogo">
          {status?.playersOnline ?? "—"}
        </dd>
        <dt>Verificado às</dt>
        <dd className="tabular text-right text-silver/80">{status?.checkedAtUtc ? formatTime(status.checkedAtUtc) : "—"}</dd>
      </dl>
      <p className="mt-2 text-sm">
        <Link href="/status" className="text-arcane hover:underline">
          Login, canais e manutenções
        </Link>
      </p>
    </GameWindow>
  );
}
