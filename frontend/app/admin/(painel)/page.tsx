import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import type { Dashboard } from "@/features/admin/types";
import { formatDateTime, formatNumber } from "@/features/home/format";
import { apiSend } from "@/services/api";

function Row({ label, value, note }: { label: string; value: React.ReactNode; note?: string }) {
  return (
    <tr className="border-t border-steel/60">
      <th scope="row" className="py-1.5 text-left font-normal text-silver/80">
        {label}
        {note ? <span className="block text-xs text-silver/50">{note}</span> : null}
      </th>
      <td className="tabular py-1.5 text-right text-data-info">{value}</td>
    </tr>
  );
}

const unavailable = <span className="text-data-bad">indisponível</span>;
const n = (v: number | null | undefined) => (v == null ? unavailable : formatNumber(v));

async function Body({ searchParams }: { searchParams: Promise<{ senha?: string }> }) {
  await connection();
  const { token } = await requireStaff("ViewDashboard");
  const sp = await searchParams;
  const r = await apiSend<Dashboard>("GET", "/api/v1/admin/dashboard", { token });
  if (!r.ok) return <p className="text-sm text-data-bad">Não foi possível carregar o resumo.</p>;
  const d = r.data;
  const server = ({ online: "Online", offline: "Offline", partial: "Parcial" } as Record<string, string>)[d.server.availability.toLowerCase()] ?? "Sem verificação";
  return (
    <>
      {sp.senha ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Senha trocada.</p> : null}
      <div className="grid gap-4 lg:grid-cols-3">
        <GameWindow title="Jogo">
          <table className="w-full text-sm">
            <tbody>
              <Row label="Contas" value={n(d.game?.accounts)} note="Soma das tabelas de conta do jogo" />
              <Row label="Personagens" value={n(d.game?.characters)} note="Nomes distintos na LevelTable" />
              <Row label="Servidor" value={<span className={server === "Online" ? "text-data-ok" : server === "Offline" ? "text-data-bad" : server === "Parcial" ? "text-data-name" : "text-silver/70"}>{server}</span>} />
              <Row label="Jogadores online" value={d.server.playersOnline ?? <span className="text-silver/60">—</span>} note="Conexões abertas na porta do servidor do jogo (GameServer:OnlinePlayers no portal.json)" />
            </tbody>
          </table>
        </GameWindow>
        <GameWindow title="Portal">
          <table className="w-full text-sm">
            <tbody>
              <Row label="Cadastros em 24 horas" value={n(d.portal?.registrations24h)} note="Feitos pelo portal novo" />
              <Row label="Cadastros em 7 dias" value={n(d.portal?.registrations7d)} />
              <Row label="Cadastros em 30 dias" value={n(d.portal?.registrations30d)} />
              <Row label="Contas com e-mail confirmado" value={n(d.portal?.accountsWithConfirmedEmail)} />
              <Row label="Chamados em andamento" value={n(d.portal?.ticketsActive)} />
              <Row label="Esperando a equipe" value={n(d.portal?.ticketsWaitingStaff)} note="Última mensagem foi do jogador" />
              <Row label="Chamados abertos em 24 horas" value={n(d.portal?.tickets24h)} />
            </tbody>
          </table>
        </GameWindow>
        <GameWindow title="Conteúdo">
          <table className="w-full text-sm">
            <tbody>
              <Row label="Notícias publicadas" value={n(d.portal?.newsPublished)} />
              <Row label="Rascunhos de notícia" value={n(d.portal?.newsDrafts)} />
              <Row label="Eventos em andamento" value={n(d.portal?.eventsLive)} />
              <Row label="Eventos agendados" value={n(d.portal?.eventsUpcoming)} />
            </tbody>
          </table>
        </GameWindow>
        <GameWindow title="Integração SQL" className="lg:col-span-3">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-silver/60">
                <th scope="col" className="py-1 font-normal">Conexão</th>
                <th scope="col" className="py-1 font-normal">Situação</th>
                <th scope="col" className="py-1 text-right font-normal">Tempo</th>
              </tr>
            </thead>
            <tbody>
              {d.integration.map((i) => (
                <tr key={i.name} className="border-t border-steel/60">
                  <td className="py-1.5">{i.name === "game-db" ? "Banco do jogo (leitura)" : i.name === "portal-db" ? "Banco do portal" : i.name}</td>
                  <td className={`py-1.5 ${i.status === "Healthy" ? "text-data-ok" : "text-data-bad"}`}>{i.status === "Healthy" ? "Respondendo" : i.status === "Degraded" ? "Lento" : "Sem resposta"}</td>
                  <td className="tabular py-1.5 text-right text-silver/70">{i.ms} ms</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-2 text-xs text-silver/50">Lido às {formatDateTime(d.generatedUtc)}.</p>
        </GameWindow>
      </div>
    </>
  );
}

export default function AdminHome({ searchParams }: PageProps<"/admin">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<{ senha?: string }>} />
    </Suspense>
  );
}
