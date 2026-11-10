import type { Metadata } from "next";
import { LogOut } from "lucide-react";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { logoutAction } from "@/features/auth/actions";
import { getCurrentPlayer } from "@/features/auth/session";
import { formatDateTime } from "@/features/home/format";

export const metadata: Metadata = { title: "Painel do jogador", robots: { index: false } };

async function AccountSummary() {
  const player = await getCurrentPlayer();
  if (!player) redirect("/login?next=/painel");
  return (
    <GameWindow title="Minha conta">
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-silver/70">Login</dt>
        <dd className="text-data-name">{player.login}</dd>
        <dt className="text-silver/70">E-mail</dt>
        <dd className="text-silver">
          {player.email ?? "não cadastrado"}{" "}
          {player.email ? (
            <span className={player.emailConfirmed ? "text-data-ok" : "text-data-bad"}>
              ({player.emailConfirmed ? "confirmado" : "não confirmado"})
            </span>
          ) : null}
        </dd>
        <dt className="text-silver/70">Sessão até</dt>
        <dd className="tabular text-data-info">{formatDateTime(player.sessionExpiresUtc)}</dd>
      </dl>
      <form action={logoutAction} className="mt-4">
        <button type="submit" className="inline-flex items-center gap-2 rounded-sm border border-bronze px-3 py-1.5 text-sm text-gold hover:border-gold">
          <LogOut className="size-4" aria-hidden />
          Sair
        </button>
      </form>
    </GameWindow>
  );
}

export default function PlayerPanelPage() {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-10">
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <AccountSummary />
      </Suspense>
    </div>
  );
}
