import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { BanForm, CoinsForm, ConfirmDialog, UnbanForm, VipAddForm, VipExtendForm, VipRemoveForm } from "@/features/admin/gameForms";
import { requireStaff } from "@/features/admin/session";
import type { AccountView, DiagnosticCheck, GameOk } from "@/features/admin/types";
import { Notice, PageTitle, Pill, Stat, type PillTone } from "@/features/admin/ui";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Conta" };

const levelTone: Record<DiagnosticCheck["level"], PillTone> = { ok: "ok", info: "info", aviso: "warn", problema: "bad" };
const levelLabel: Record<DiagnosticCheck["level"], string> = { ok: "OK", info: "Info", aviso: "Atenção", problema: "Problema" };
const localDate = (s: string | null) => (s ? `${s.slice(8, 10)}/${s.slice(5, 7)}/${s.slice(0, 4)} ${s.slice(11, 16)}` : "—");
const when = (s: string | null) => (s ? formatDateTime(s) : "—");

async function Body({ params }: { params: Promise<{ login: string }> }) {
  await connection();
  const login = decodeURIComponent((await params).login);
  const { me, token } = await requireStaff("ViewAccounts");
  const r = await apiSend<GameOk<AccountView>>("GET", `/api/v1/admin/accounts/${encodeURIComponent(login)}`, { token });
  if (!r.ok) {
    return (
      <>
        <PageTitle title={login} />
        <Notice tone={r.error.code === "disabled" ? "warn" : "bad"}>{r.error.message}</Notice>
      </>
    );
  }
  const a = r.data.data;
  const can = (c: string) => me.capabilities.includes(c as never);
  const problems = a.diagnostics.filter((d) => d.level === "problema").length;
  const warnings = a.diagnostics.filter((d) => d.level === "aviso").length;
  const activeBan = a.bans.find((b) => !b.liftedUtc);

  return (
    <>
      <PageTitle
        title={a.login}
        actions={
          can("ManageAccounts") ? (
            a.blocked ? (
              <ConfirmDialog label="Desbanir" title={`Desbanir ${a.login}`} tone="calm">
                <UnbanForm login={a.login} />
              </ConfirmDialog>
            ) : (
              <ConfirmDialog label="Banir conta" title={`Banir ${a.login}`} tone="danger">
                <BanForm login={a.login} />
              </ConfirmDialog>
            )
          ) : null
        }
      >
        <Link href="/admin/contas" className="text-arcane hover:underline">
          ← Voltar para a busca
        </Link>
      </PageTitle>

      <div className="mb-4 flex flex-wrap gap-2">
        {a.deleted ? <Pill tone="bad">Apagada</Pill> : null}
        {a.blocked ? <Pill tone="bad">Banida{activeBan?.expiresUtc ? ` até ${when(activeBan.expiresUtc)}` : ""}</Pill> : <Pill tone="ok">Liberada</Pill>}
        {a.gameMasterLevel != null ? <Pill tone="info">GM nível {a.gameMasterLevel}</Pill> : null}
        {a.vips.some((v) => v.active) ? <Pill tone="warn">VIP ativo</Pill> : null}
        {problems > 0 ? <Pill tone="bad">{problems} problema(s)</Pill> : null}
        {warnings > 0 ? <Pill tone="warn">{warnings} atenção</Pill> : null}
      </div>

      <div className="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Coins" value={a.coins == null ? "—" : a.coins.toLocaleString("pt-BR")} />
        <Stat label="Personagens" value={a.characters.length} />
        <Stat label="Criada em" value={when(a.registeredUtc)} />
        <Stat label="E-mail no site" value={a.portal?.email ?? "—"} note={a.portal?.email ? (a.portal.emailConfirmedUtc ? "confirmado" : "não confirmado") : undefined} />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <GameWindow title="Diagnóstico" className="xl:col-span-2">
          <ul className="grid gap-2 md:grid-cols-2">
            {a.diagnostics.map((d) => (
              <li key={d.key} className="flex gap-3 rounded-sm border border-steel/60 bg-night/40 p-2.5">
                <span className="shrink-0">
                  <Pill tone={levelTone[d.level]}>{levelLabel[d.level]}</Pill>
                </span>
                <span>
                  <span className="block text-sm text-silver">{d.title}</span>
                  <span className="block text-xs text-silver/60">{d.detail}</span>
                </span>
              </li>
            ))}
          </ul>
        </GameWindow>

        <GameWindow title="Personagens">
          {a.characters.length === 0 ? (
            <p className="text-sm text-silver/60">Nenhum personagem.</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/55">
                  <th scope="col" className="py-1 font-normal">Nome</th>
                  <th scope="col" className="py-1 font-normal">Classe</th>
                  <th scope="col" className="py-1 text-right font-normal">Nível</th>
                  <th scope="col" className="py-1 pl-3 font-normal">Clã</th>
                </tr>
              </thead>
              <tbody>
                {a.characters.map((c) => (
                  <tr key={c.name} className="border-t border-steel/60">
                    <td className="py-1.5 text-data-name">{c.name}</td>
                    <td className="py-1.5 text-silver/75">{c.className}</td>
                    <td className="tabular py-1.5 text-right">{c.level ?? "—"}</td>
                    <td className="py-1.5 pl-3 text-silver/65">{c.clan ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {a.pvp.length > 0 ? (
            <p className="mt-3 text-xs text-silver/55">
              PvP: {a.pvp.map((p) => `${p.nick ?? "?"} ${p.battlePoints ?? 0} BP (${p.weeklyBattlePoints} na semana), ${p.kills} abates / ${p.deaths} mortes`).join(" · ")}
            </p>
          ) : null}
        </GameWindow>

        <GameWindow
          title="Coins"
          actions={
            can("ManageEconomy") ? (
              <ConfirmDialog label="Mudar coins" title={`Coins de ${a.login}`} small>
                <CoinsForm login={a.login} current={a.coins} />
              </ConfirmDialog>
            ) : null
          }
        >
          <p className="tabular text-3xl text-data-name">{a.coins == null ? "—" : a.coins.toLocaleString("pt-BR")}</p>
          <p className="mt-1 text-xs text-silver/55">Saldo em CoinShopDB.UserInfo. Toda mudança fica na auditoria com o antes e o depois.</p>
        </GameWindow>

        <GameWindow
          title="VIP"
          className="xl:col-span-2"
          actions={
            can("ManageEconomy") ? (
              <ConfirmDialog label="Adicionar VIP" title={`Adicionar VIP para ${a.login}`} small>
                <VipAddForm login={a.login} plans={a.vipPlans} characters={a.characters.map((c) => c.name)} />
              </ConfirmDialog>
            ) : null
          }
        >
          {a.vips.length === 0 ? (
            <p className="text-sm text-silver/60">Nunca teve VIP.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-sm">
                <thead>
                  <tr className="text-left text-xs text-silver/55">
                    <th scope="col" className="py-1 font-normal">Plano</th>
                    <th scope="col" className="py-1 font-normal">Personagem</th>
                    <th scope="col" className="py-1 font-normal">Início</th>
                    <th scope="col" className="py-1 font-normal">Fim</th>
                    <th scope="col" className="py-1 font-normal">Situação</th>
                    <th scope="col" className="py-1 font-normal">
                      <span className="sr-only">Ações</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {a.vips.map((v, i) => (
                    <tr key={`${v.startLocal}-${i}`} className="border-t border-steel/60">
                      <td className="py-1.5">{v.planLabel}</td>
                      <td className="py-1.5 text-silver/70">{v.playerName ?? "conta"}</td>
                      <td className="tabular py-1.5 text-silver/70">{localDate(v.startLocal)}</td>
                      <td className="tabular py-1.5 text-silver/70">{localDate(v.endLocal)}</td>
                      <td className="py-1.5">{v.active ? <Pill tone="ok">Ativo</Pill> : <Pill tone="mute">Vencido</Pill>}</td>
                      <td className="py-1.5">
                        {can("ManageEconomy") ? (
                          <span className="flex justify-end gap-1.5">
                            <ConfirmDialog label="Estender" title="Estender VIP" small tone="calm">
                              <VipExtendForm login={a.login} vip={v} />
                            </ConfirmDialog>
                            <ConfirmDialog label="Remover" title="Remover VIP" small tone="danger">
                              <VipRemoveForm login={a.login} vip={v} />
                            </ConfirmDialog>
                          </span>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="mt-2 text-xs text-silver/50">Datas no horário de Brasília, como o jogo grava.</p>
        </GameWindow>

        <GameWindow title="Banimentos">
          {a.bans.length === 0 ? (
            <p className="text-sm text-silver/60">Nenhum banimento registrado pelo painel.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {a.bans.map((b) => (
                <li key={b.id} className="rounded-sm border border-steel/60 bg-night/40 p-2.5 text-sm">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-silver/80">
                      {when(b.bannedUtc)} por <strong>{b.bannedBy}</strong>
                    </span>
                    {b.liftedUtc ? <Pill tone="mute">Encerrado</Pill> : <Pill tone="bad">Ativo</Pill>}
                  </div>
                  <p className="mt-1 text-silver/70">{b.reason}</p>
                  <p className="mt-1 text-xs text-silver/50">
                    {b.expiresUtc ? `Prazo: ${when(b.expiresUtc)}. ` : "Sem prazo. "}
                    {b.liftedUtc ? `Liberado em ${when(b.liftedUtc)} por ${b.liftedBy}: ${b.liftReason ?? ""}` : null}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </GameWindow>

        <GameWindow title="Atividade recente">
          {a.activity.length === 0 ? (
            <p className="text-sm text-silver/60">Nada registrado.</p>
          ) : (
            <ul className="flex flex-col divide-y divide-steel/50 text-sm">
              {a.activity.map((e) => (
                <li key={e.id} className="flex flex-wrap justify-between gap-2 py-1.5">
                  <span className="font-mono text-xs text-data-info">{e.action}</span>
                  <span className="text-xs text-silver/55">
                    {e.actorType === "staff" ? `equipe: ${e.actorId}` : e.actorType === "player" ? "jogador" : "sistema"} · {when(e.occurredUtc)}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <p className="mt-2 text-xs text-silver/50">
            Sessões abertas no site: {a.activePortalSessions}. Último acesso ao site: {when(a.portal?.lastLoginUtc ?? null)}.
          </p>
        </GameWindow>
      </div>
    </>
  );
}

export default function AccountPage({ params }: PageProps<"/admin/contas/[login]">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body params={params} />
    </Suspense>
  );
}
