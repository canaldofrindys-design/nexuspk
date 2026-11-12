import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatDateTime, formatTime } from "@/features/home/format";
import type { StatusReport } from "@/features/support/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = {
  title: "Status do servidor",
  description: "Login e canais do servidor testados a cada minuto, avisos e manutenções programadas.",
};

const overall = {
  online: { text: "Servidor online", className: "text-data-ok", dot: "bg-data-ok" },
  partial: { text: "Parte do servidor fora do ar", className: "text-data-name", dot: "bg-data-name" },
  offline: { text: "Servidor offline", className: "text-data-bad", dot: "bg-data-bad" },
  unknown: { text: "Sem verificação configurada", className: "text-silver/70", dot: "bg-silver/50" },
} as const;

function pct(v: number | null) {
  return v === null ? "—" : `${(Math.floor(v * 1000) / 10).toLocaleString("pt-BR", { minimumFractionDigits: 1 })}%`;
}

function cellClass(v: number | null) {
  if (v === null) return "bg-steel/40";
  if (v >= 0.999) return "bg-data-ok";
  if (v >= 0.5) return "bg-data-name";
  return "bg-data-bad";
}

function hourLabel(iso: string, v: number | null) {
  const h = formatTime(iso);
  return v === null ? `${h}: sem medição` : `${h}: ${pct(v)} das verificações online`;
}

async function Body() {
  await connection();
  const r = await apiSend<StatusReport>("GET", "/api/v1/status");
  if (!r.ok) {
    return (
      <GameWindow title="Servidor">
        <p className="text-sm text-data-bad">A API do portal não respondeu. Não dá para afirmar o estado do servidor agora.</p>
      </GameWindow>
    );
  }
  const s = r.data;
  const state = overall[s.availability] ?? overall.unknown;
  const active = s.notices.filter((n) => n.active);
  const upcoming = s.notices.filter((n) => !n.active);

  return (
    <div className="flex flex-col gap-4">
      {active.map((n) => (
        <div key={n.id} role="status" className={`border px-4 py-3 ${n.kind === "manutencao" ? "border-data-bad/70 bg-data-bad/10" : "border-gold/60 bg-gold/10"}`}>
          <p className={`font-semibold ${n.kind === "manutencao" ? "text-data-bad" : "text-gold-bright"}`}>
            {n.kind === "manutencao" ? "Em manutenção: " : "Aviso: "}
            {n.title}
          </p>
          <p className="mt-1 whitespace-pre-wrap text-sm text-silver">{n.body}</p>
          {n.endsUtc ? <p className="mt-1 text-xs text-silver/70">Previsão de término: {formatDateTime(n.endsUtc)} (horário de Brasília)</p> : null}
        </div>
      ))}

      <div className="grid gap-4 lg:grid-cols-[1fr_320px]">
        <GameWindow title="Login e canais" className="min-w-0">
          <p className={`mb-3 flex items-center gap-2 text-lg font-semibold ${state.className}`}>
            <span className={`inline-block size-2.5 rounded-full ${state.dot}`} aria-hidden />
            {s.maintenanceNow && s.availability !== "online" ? "Em manutenção programada" : state.text}
          </p>
          {s.targets.length === 0 ? (
            <p className="text-sm text-silver/70">O teste de porta do login e dos canais ainda não foi configurado no servidor do portal.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-sm">
                <caption className="sr-only">Estado de cada endereço testado</caption>
                <thead>
                  <tr className="text-left text-xs text-silver/60">
                    <th scope="col" className="py-1 font-normal">Endereço</th>
                    <th scope="col" className="py-1 font-normal">Agora</th>
                    <th scope="col" className="py-1 text-right font-normal">Resposta</th>
                    <th scope="col" className="py-1 text-right font-normal">24 horas</th>
                    <th scope="col" className="py-1 text-right font-normal">7 dias</th>
                  </tr>
                </thead>
                <tbody>
                  {s.targets.map((t) => (
                    <tr key={t.key} className="border-t border-steel/60 align-top">
                      <td className="py-2">
                        <span className="font-medium text-data-name">{t.name}</span>
                        <ol className="mt-1.5 flex gap-px" aria-label={`Últimas 24 horas de ${t.name}`}>
                          {t.last24h.map((c) => (
                            <li key={c.hourUtc} title={hourLabel(c.hourUtc, c.online)} className={`h-4 w-2 ${cellClass(c.online)}`}>
                              <span className="sr-only">{hourLabel(c.hourUtc, c.online)}</span>
                            </li>
                          ))}
                        </ol>
                      </td>
                      <td className={`py-2 font-semibold ${t.online === null ? "text-silver/60" : t.online ? "text-data-ok" : "text-data-bad"}`}>
                        {t.online === null ? "—" : t.online ? "Online" : "Offline"}
                      </td>
                      <td className="tabular py-2 text-right text-data-info">{t.latencyMs === null ? "—" : `${t.latencyMs} ms`}</td>
                      <td className="tabular py-2 text-right text-data-info">{pct(t.uptime24h)}</td>
                      <td className="tabular py-2 text-right text-data-info">{pct(t.uptime7d)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="mt-3 text-xs text-silver/60">
            O portal abre uma conexão com a porta de cada endereço a cada minuto. Porta aberta quer dizer que o programa está no ar, não que o jogo esteja sem lentidão.
            {s.historySinceUtc ? ` Histórico medido desde ${formatDateTime(s.historySinceUtc)}.` : " O histórico começa quando o monitoramento é ligado; ainda não há medições gravadas."}
          </p>
        </GameWindow>

        <div className="flex flex-col gap-4">
          <GameWindow title="Resumo">
            <dl className="grid grid-cols-[1fr_auto] gap-y-1.5 text-sm">
              <dt>Verificado às</dt>
              <dd className="tabular text-right text-silver/80">{s.checkedUtc ? formatTime(s.checkedUtc) : "—"}</dd>
              <dt>Jogadores online</dt>
              <dd className="tabular text-right text-data-info">{s.playersOnline ?? "—"}</dd>
            </dl>
            <p className="mt-2 text-xs text-silver/60">
              {s.playersOnline === null
                ? "Sem número enquanto a contagem não está ligada ou o servidor não responde."
                : "Jogadores conectados ao servidor do jogo agora. Atualiza a cada 30 segundos."}
            </p>
          </GameWindow>
          <GameWindow title="Próximas manutenções e avisos">
            {upcoming.length === 0 ? (
              <p className="text-sm text-silver/70">Nada programado.</p>
            ) : (
              <ul className="flex flex-col gap-3 text-sm">
                {upcoming.map((n) => (
                  <li key={n.id}>
                    <p className="font-semibold text-silver">
                      <span className={n.kind === "manutencao" ? "text-data-bad" : "text-gold-bright"}>{n.kind === "manutencao" ? "Manutenção" : "Aviso"}</span> {n.title}
                    </p>
                    <p className="tabular text-xs text-silver/70">
                      {formatDateTime(n.startsUtc)}
                      {n.endsUtc ? ` até ${formatDateTime(n.endsUtc)}` : ""}
                    </p>
                    <p className="mt-1 whitespace-pre-wrap text-silver/80">{n.body}</p>
                  </li>
                ))}
              </ul>
            )}
          </GameWindow>
          <GameWindow title="Problema na sua conta?">
            <p className="text-sm text-silver/80">
              Se o servidor está online e você não consegue entrar,{" "}
              <Link href="/painel/suporte/novo" className="text-arcane hover:underline">
                abra um chamado
              </Link>
              .
            </p>
          </GameWindow>
        </div>
      </div>
    </div>
  );
}

export default function StatusPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Status do servidor</h1>
      <Suspense fallback={<p className="text-sm text-silver/60">Verificando…</p>}>
        <Body />
      </Suspense>
      <p className="mt-4 text-xs text-silver/50">Horários de Brasília.</p>
    </div>
  );
}
