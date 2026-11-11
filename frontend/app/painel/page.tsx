import type { Metadata } from "next";
import { LogOut } from "lucide-react";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { logoutAction } from "@/features/auth/actions";
import { getSessionToken } from "@/features/auth/session";
import { formatDate, formatDateTime, formatNumber } from "@/features/home/format";
import { ChangeEmailForm, ChangePasswordForm, ResendConfirmationButton, RevokeOthersButton } from "@/features/player/forms";
import type { PlayerPanel } from "@/features/player/types";
import { clientContext } from "@/features/auth/session";
import { apiSend } from "@/services/api";
import Link from "next/link";
import { PlayerTicketTable } from "@/features/support/PlayerTickets";
import type { TicketList } from "@/features/support/types";

export const metadata: Metadata = { title: "Painel do jogador", robots: { index: false } };

async function loadPanel(): Promise<PlayerPanel | "error"> {
  const token = await getSessionToken();
  if (!token) redirect("/login?next=/painel");
  const result = await apiSend<PlayerPanel>("GET", "/api/v1/player/panel", { token, ...(await clientContext()) });
  if (!result.ok && result.status === 401) redirect("/login?next=/painel");
  return result.ok ? result.data : "error";
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-silver/70">{label}</dt>
      <dd className="text-right text-silver">{children}</dd>
    </>
  );
}

function CharactersWindow({ panel }: { panel: PlayerPanel }) {
  const characters = panel.game?.characters ?? [];
  return (
    <GameWindow title="Personagens">
      {!panel.gameDataAvailable ? (
        <p className="text-sm text-silver/70">Os dados do jogo estão indisponíveis no momento. Tente de novo em alguns minutos.</p>
      ) : characters.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum personagem encontrado nesta conta.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] text-sm">
            <caption className="sr-only">Personagens da conta</caption>
            <thead>
              <tr className="text-left text-xs text-silver/60">
                <th scope="col" className="py-1 font-normal">Personagem</th>
                <th scope="col" className="py-1 font-normal">Classe</th>
                <th scope="col" className="py-1 text-right font-normal">Nível</th>
                <th scope="col" className="py-1 pl-4 font-normal">Clã</th>
                <th scope="col" className="py-1 text-right font-normal">Abates / mortes PvP</th>
                <th scope="col" className="py-1 text-right font-normal">BP</th>
              </tr>
            </thead>
            <tbody>
              {characters.map((c) => (
                <tr key={c.name} className="border-t border-steel/60">
                  <td className="py-1.5 font-medium text-data-name">{c.name}</td>
                  <td className="py-1.5 text-silver/80">{c.className}</td>
                  <td className="tabular py-1.5 text-right text-data-info">{c.level}</td>
                  <td className="py-1.5 pl-4 text-silver/80">{c.clan ?? "—"}</td>
                  <td className="tabular py-1.5 text-right text-data-info">
                    {c.pvpKills === null ? "—" : `${formatNumber(c.pvpKills)} / ${formatNumber(c.pvpDeaths ?? 0)}`}
                  </td>
                  <td className="tabular py-1.5 text-right text-data-info">{c.battlePoints === null ? "—" : formatNumber(c.battlePoints)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="mt-2 text-xs text-silver/50">Nível como o servidor grava hoje. EXP ainda não é publicada pelo servidor.</p>
    </GameWindow>
  );
}

function AccountWindow({ panel }: { panel: PlayerPanel }) {
  const game = panel.game;
  const vip = game?.vip;
  return (
    <GameWindow title="Conta">
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <Row label="Login">
          <span className="text-data-name">{panel.login}</span>
        </Row>
        <Row label="Situação">
          {game === null ? "—" : game.blocked ? <span className="text-data-bad">bloqueada</span> : <span className="text-data-ok">ativa</span>}
        </Row>
        <Row label="Criada em">{game?.registeredUtc ? <span className="tabular">{formatDate(game.registeredUtc)}</span> : "—"}</Row>
        <Row label="VIP">
          {!vip ? (
            "não"
          ) : vip.active ? (
            <span className="text-data-ok">
              ativo até <span className="tabular">{vip.endsUtc ? formatDate(vip.endsUtc) : "—"}</span>
            </span>
          ) : (
            <span className="text-silver/70">
              expirou em <span className="tabular">{vip.endsUtc ? formatDate(vip.endsUtc) : "—"}</span>
            </span>
          )}
        </Row>
        <Row label="Coins">
          <span className="tabular text-data-info">{game?.coins === null || game?.coins === undefined ? "—" : formatNumber(game.coins)}</span>
        </Row>
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

function EmailWindow({ panel }: { panel: PlayerPanel }) {
  return (
    <GameWindow title="E-mail">
      <p className="mb-3 text-sm text-silver/80">
        {panel.email ? (
          <>
            {panel.email}{" "}
            <span className={panel.emailConfirmed ? "text-data-ok" : "text-data-bad"}>({panel.emailConfirmed ? "confirmado" : "não confirmado"})</span>
          </>
        ) : (
          "Nenhum e-mail cadastrado. Sem e-mail confirmado não dá para recuperar a conta sozinho."
        )}
      </p>
      {panel.email && !panel.emailConfirmed && panel.emailSendingAvailable ? (
        <div className="mb-3">
          <ResendConfirmationButton />
        </div>
      ) : null}
      <ChangeEmailForm current={panel.email} />
    </GameWindow>
  );
}

function SecurityWindow({ panel }: { panel: PlayerPanel }) {
  return (
    <GameWindow title="Trocar senha">
      {panel.passwordChangeAvailable ? (
        <ChangePasswordForm />
      ) : (
        <p className="text-sm text-silver/70">A troca de senha pelo site está desligada no momento.</p>
      )}
    </GameWindow>
  );
}

function AccessWindow({ panel }: { panel: PlayerPanel }) {
  return (
    <GameWindow title="Acessos recentes ao site">
      {panel.recentLogins.length === 0 ? (
        <p className="mb-3 text-sm text-silver/70">Nenhum acesso registrado.</p>
      ) : (
        <table className="mb-3 w-full text-sm">
          <caption className="sr-only">Últimos acessos ao painel</caption>
          <thead>
            <tr className="text-left text-xs text-silver/60">
              <th scope="col" className="py-1 font-normal">Quando</th>
              <th scope="col" className="py-1 text-right font-normal">IP</th>
            </tr>
          </thead>
          <tbody>
            {panel.recentLogins.map((l) => (
              <tr key={l.atUtc} className="border-t border-steel/60">
                <td className="tabular py-1.5 text-silver">{formatDateTime(l.atUtc)}</td>
                <td className="tabular py-1.5 text-right text-silver/70">{l.ipAddress ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <RevokeOthersButton />
    </GameWindow>
  );
}

async function SupportWindow() {
  const token = await getSessionToken();
  const r = token ? await apiSend<TicketList>("GET", "/api/v1/player/tickets", { token, ...(await clientContext()) }) : null;
  const active = r?.ok ? r.data.items.filter((t) => t.status !== "resolvido" && t.status !== "fechado").slice(0, 3) : [];
  return (
    <GameWindow title="Suporte">
      {!r?.ok ? (
        <p className="text-sm text-silver/70">Não foi possível carregar os chamados agora.</p>
      ) : active.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum chamado em andamento.</p>
      ) : (
        <PlayerTicketTable items={active} />
      )}
      <p className="mt-2 flex flex-wrap gap-4 text-sm">
        <Link href="/painel/suporte/novo" className="text-arcane hover:underline">
          Abrir chamado
        </Link>
        <Link href="/painel/suporte" className="text-arcane hover:underline">
          Todos os chamados{r?.ok && r.data.total > 0 ? ` (${r.data.total})` : ""}
        </Link>
      </p>
    </GameWindow>
  );
}

async function Panel() {
  const panel = await loadPanel();
  if (panel === "error") {
    return (
      <GameWindow title="Painel do jogador">
        <p className="text-sm text-data-bad">Não foi possível carregar o painel. Tente de novo em instantes.</p>
      </GameWindow>
    );
  }
  return (
    <div className="grid gap-4 lg:grid-cols-[1fr_340px]">
      <div className="flex min-w-0 flex-col gap-4">
        <CharactersWindow panel={panel} />
        <Suspense fallback={null}>
          <SupportWindow />
        </Suspense>
        <AccessWindow panel={panel} />
      </div>
      <div className="flex flex-col gap-4">
        <AccountWindow panel={panel} />
        <SecurityWindow panel={panel} />
        <EmailWindow panel={panel} />
      </div>
    </div>
  );
}

export default function PlayerPanelPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Painel do jogador</h1>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Panel />
      </Suspense>
    </div>
  );
}
