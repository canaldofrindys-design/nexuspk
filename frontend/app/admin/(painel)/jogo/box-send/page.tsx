import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { DonateBoxForm } from "@/features/admin/DonateBoxForm";
import { ItemThumb } from "@/features/admin/ItemPicker";
import { requireStaff } from "@/features/admin/session";
import type { AccountHit, AccountView, CatalogItem, GameOk, ItemCatalog } from "@/features/admin/types";
import { Notice, PageTitle, Pill, type PillTone } from "@/features/admin/ui";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Enviar Box Donate" };

type Sent = {
  id: number;
  login: string;
  character: string | null;
  spec: number;
  status: string;
  createdBy: string | null;
  reason: string | null;
  createdAt: string | null;
  completedAt: string | null;
  notified: boolean;
  items: number;
  firstItem: string | null;
};
type Overview = {
  runtime: { ready: boolean; lastSeenLocal: string | null; protocolVersion: number } | null;
  online: boolean;
  recent: Sent[];
  maxItems: number;
  maxAging: number;
};

const statusInfo: Record<string, { label: string; tone: PillTone }> = {
  PENDENTE: { label: "Esperando o jogador", tone: "warn" },
  CAIXA: { label: "Caixa no inventário", tone: "info" },
  ABRINDO: { label: "Abrindo", tone: "info" },
  CONCLUIDO: { label: "Entregue", tone: "ok" },
  CANCELADO: { label: "Cancelado", tone: "bad" },
};

/** Datas do jogo vêm no horário de Brasília, sem fuso. */
const localDate = (s: string | null) => (s ? `${s.slice(8, 10)}/${s.slice(5, 7)} ${s.slice(11, 16)}` : "—");

async function Body({ searchParams }: { searchParams: Promise<{ conta?: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageEconomy");
  const q = ((await searchParams).conta ?? "").trim().slice(0, 32);

  const [overview, cat] = await Promise.all([
    apiSend<GameOk<Overview>>("GET", "/api/v1/admin/donate-box", { token }),
    apiSend<ItemCatalog>("GET", "/api/v1/admin/items", { token }),
  ]);
  if (!overview.ok) return <Notice tone={overview.error.code === "disabled" ? "warn" : "bad"}>{overview.error.message}</Notice>;
  const o = overview.data.data;
  const catalog: CatalogItem[] = cat.ok ? cat.data.items : [];
  const byId = new Map(catalog.map((i) => [i.id, i]));

  let account: AccountView | null = null;
  let hits: AccountHit[] = [];
  let lookupError: string | null = null;
  if (q.length >= 2) {
    const direct = await apiSend<GameOk<AccountView>>("GET", `/api/v1/admin/accounts/${encodeURIComponent(q)}`, { token });
    if (direct.ok) {
      account = direct.data.data;
    } else {
      const found = await apiSend<GameOk<AccountHit[]>>("GET", `/api/v1/admin/accounts?${new URLSearchParams({ q })}`, { token });
      if (found.ok) {
        hits = found.data.data;
        if (hits.length === 1) {
          const one = await apiSend<GameOk<AccountView>>("GET", `/api/v1/admin/accounts/${encodeURIComponent(hits[0].login)}`, { token });
          if (one.ok) account = one.data.data;
        }
        if (!account && hits.length === 0) lookupError = "Nenhuma conta ou personagem com esse nome.";
      } else {
        lookupError = found.error.message;
      }
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {!o.online ? (
        <Notice title="O distribuidor do jogo não está respondendo">
          {o.runtime?.lastSeenLocal
            ? `Último sinal do servidor às ${localDate(o.runtime.lastSeenLocal)}. `
            : "O servidor ainda não deu sinal. "}
          A caixa fica guardada e é entregue quando o servidor do jogo voltar.
        </Notice>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <GameWindow title="Montar a caixa">
          <form method="get" className="mb-4 flex flex-wrap items-end gap-2">
            <div className="flex min-w-48 flex-1 flex-col gap-1">
              <label htmlFor="f-conta" className="text-sm text-silver">
                Conta ou personagem
              </label>
              <input
                id="f-conta"
                name="conta"
                defaultValue={q}
                maxLength={32}
                className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane"
              />
            </div>
            <button type="submit" className="rounded-sm border border-arcane/70 px-4 py-2 text-sm text-arcane hover:bg-arcane/10">
              Procurar
            </button>
          </form>

          {lookupError ? <Notice tone="bad">{lookupError}</Notice> : null}

          {!account && hits.length > 1 ? (
            <ul className="flex flex-col gap-1 text-sm">
              {hits.map((h) => (
                <li key={h.login}>
                  <Link href={`/admin/jogo/box-send?${new URLSearchParams({ conta: h.login })}`} className="text-arcane hover:underline">
                    {h.login}
                  </Link>
                  {h.matchedCharacter ? <span className="text-silver/60"> · {h.matchedCharacter}</span> : null}
                </li>
              ))}
            </ul>
          ) : null}

          {account ? (
            <div className="flex flex-col gap-3">
              <p className="text-sm text-silver/70">
                Conta{" "}
                <Link href={`/admin/contas/${encodeURIComponent(account.login)}`} className="text-data-name hover:underline">
                  {account.login}
                </Link>
                {account.blocked ? (
                  <span className="ml-2">
                    <Pill tone="bad">Banida</Pill>
                  </span>
                ) : null}
              </p>
              {account.characters.length === 0 ? (
                <Notice>Esta conta ainda não tem personagem. A caixa é entregue a um personagem.</Notice>
              ) : (
                <DonateBoxForm login={account.login} characters={account.characters} catalog={catalog} maxItems={o.maxItems} maxAging={o.maxAging} />
              )}
            </div>
          ) : null}

          {!account && hits.length === 0 && !lookupError ? (
            <p className="text-sm text-silver/60">Procure a conta (ou o nome do personagem) para montar a caixa.</p>
          ) : null}
        </GameWindow>

        <GameWindow title="Como funciona">
          <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-silver/75">
            <li>O portal cria o pacote no sistema de donate do servidor, o mesmo que a Coin Shop e o Login Diário usam.</li>
            <li>O servidor do jogo monta os itens, avisa o jogador e entrega a Box Donate quando ele entra com o personagem escolhido.</li>
            <li>A classe do personagem vai junto, para o servidor montar os itens certos.</li>
            <li>Uma caixa leva até {o.maxItems} itens. Todo envio fica na auditoria com o motivo e o seu nome.</li>
          </ul>
        </GameWindow>
      </div>

      <GameWindow title="Últimas caixas enviadas pelo portal">
        {o.recent.length === 0 ? (
          <p className="text-sm text-silver/60">Nenhuma ainda.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/55">
                  <th scope="col" className="py-1 font-normal">Nº</th>
                  <th scope="col" className="py-1 font-normal">Conta / personagem</th>
                  <th scope="col" className="py-1 font-normal">Itens</th>
                  <th scope="col" className="py-1 font-normal">Situação</th>
                  <th scope="col" className="py-1 font-normal">Criada</th>
                  <th scope="col" className="py-1 font-normal">Motivo</th>
                </tr>
              </thead>
              <tbody>
                {o.recent.map((p) => {
                  const s = statusInfo[p.status] ?? { label: p.status, tone: "info" as PillTone };
                  const first = p.firstItem ? byId.get(p.firstItem.toUpperCase()) : undefined;
                  return (
                    <tr key={p.id} className="border-t border-steel/60 align-top">
                      <td className="tabular py-1.5 text-silver/60">{p.id}</td>
                      <td className="py-1.5">
                        <Link href={`/admin/contas/${encodeURIComponent(p.login)}`} className="text-data-name hover:underline">
                          {p.login}
                        </Link>
                        {p.character ? <span className="block text-xs text-silver/60">{p.character}</span> : null}
                      </td>
                      <td className="py-1.5">
                        <span className="flex items-center gap-2">
                          {first ? <ItemThumb id={first.id} image={first.image} size={24} /> : null}
                          <span>
                            {first?.name ?? p.firstItem ?? "—"}
                            {p.items > 1 ? <span className="text-silver/55"> + {p.items - 1}</span> : null}
                          </span>
                        </span>
                      </td>
                      <td className="py-1.5">
                        <Pill tone={s.tone}>{s.label}</Pill>
                        {p.status === "PENDENTE" && p.notified ? <span className="block text-xs text-silver/50">jogador avisado</span> : null}
                      </td>
                      <td className="tabular py-1.5 text-silver/65">
                        {localDate(p.createdAt)}
                        {p.createdBy ? <span className="block text-xs text-silver/45">{p.createdBy}</span> : null}
                      </td>
                      <td className="max-w-56 py-1.5 text-xs text-silver/60">{p.reason ?? "—"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </GameWindow>
    </div>
  );
}

export default function DonateBoxPage({ searchParams }: { searchParams: Promise<{ conta?: string }> }) {
  return (
    <>
      <PageTitle title="Enviar Box Donate">Entrega uma Box Donate com os itens que você escolher para o personagem do jogador.</PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body searchParams={searchParams} />
      </Suspense>
    </>
  );
}
