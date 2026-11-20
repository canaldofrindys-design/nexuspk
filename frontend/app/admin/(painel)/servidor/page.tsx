import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { ConfirmDialog, ServerActionForm } from "@/features/admin/gameForms";
import { requireStaff } from "@/features/admin/session";
import type { ServerPage, ServerProcess } from "@/features/admin/types";
import { Notice, PageTitle, Pill, type PillTone } from "@/features/admin/ui";
import { formatDateTime } from "@/features/home/format";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Ligar / reiniciar" };

const stateView: Record<ServerProcess["state"], { tone: PillTone; label: string }> = {
  running: { tone: "ok", label: "Ligado" },
  runningOtherOwner: { tone: "warn", label: "Ligado (outra conta)" },
  stopped: { tone: "bad", label: "Desligado" },
  missing: { tone: "mute", label: "Arquivo não encontrado" },
};

const warnings = {
  start: "O programa será aberto na VPS.",
  stop: "Quem estiver jogando nesse servidor cai. Personagens são salvos pelo próprio servidor ao fechar; se ele não fechar no prazo, é encerrado à força.",
  restart: "Quem estiver jogando nesse servidor cai e precisa entrar de novo depois que ele voltar.",
};

async function Body() {
  await connection();
  const { token } = await requireStaff("ControlServer");
  const r = await apiSend<ServerPage>("GET", "/api/v1/admin/server", { token });
  if (!r.ok) return <Notice tone="bad">{r.error.message}</Notice>;
  const d = r.data;
  if (!d.enabled) {
    return (
      <Notice title="Nenhum programa configurado">
        Cadastre os programas do servidor (login e canais) em <code>GameServerControl:Processes</code> no portal.json da VPS. O passo a passo está em docs/ADMIN.md, seção
        &quot;Ligar / reiniciar&quot;.
      </Notice>
    );
  }
  const words = d.confirmWords;
  return (
    <div className="flex flex-col gap-4">
      <GameWindow
        title="Todos os programas"
        actions={
          <span className="flex flex-wrap gap-2">
            <ConfirmDialog label="Ligar todos" title="Ligar todos (na ordem)" tone="calm" small>
              <ServerActionForm target="*" action="start" word={words.start} warning="Liga na ordem da lista, esperando alguns segundos entre um e outro." />
            </ConfirmDialog>
            <ConfirmDialog label="Reiniciar todos" title="Reiniciar todos" tone="danger" small>
              <ServerActionForm target="*" action="restart" word={words.restart} warning={warnings.restart} />
            </ConfirmDialog>
            <ConfirmDialog label="Desligar todos" title="Desligar todos" tone="danger" small>
              <ServerActionForm target="*" action="stop" word={words.stop} warning={warnings.stop} />
            </ConfirmDialog>
          </span>
        }
      >
        <ul className="grid gap-3 md:grid-cols-2">
          {d.processes.map((p) => {
            const s = stateView[p.state];
            return (
              <li key={p.key} className="rounded-md border border-steel/70 bg-night/50 p-3">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-silver">{p.name}</span>
                  <Pill tone={s.tone}>{s.label}</Pill>
                </div>
                <p className="mt-1 text-xs text-silver/55">
                  {p.startedUtc ? `Ligado desde ${formatDateTime(p.startedUtc)}. ` : ""}
                  {p.note ?? ""}
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <ConfirmDialog label="Ligar" title={`Ligar ${p.name}`} tone="calm" small>
                    <ServerActionForm target={p.key} action="start" word={words.start} warning={warnings.start} />
                  </ConfirmDialog>
                  <ConfirmDialog label="Reiniciar" title={`Reiniciar ${p.name}`} tone="gold" small>
                    <ServerActionForm target={p.key} action="restart" word={words.restart} warning={warnings.restart} />
                  </ConfirmDialog>
                  <ConfirmDialog label="Desligar" title={`Desligar ${p.name}`} tone="danger" small>
                    <ServerActionForm target={p.key} action="stop" word={words.stop} warning={warnings.stop} />
                  </ConfirmDialog>
                </div>
              </li>
            );
          })}
        </ul>
      </GameWindow>
      <p className="text-xs text-silver/50">
        Só o superadministrador vê esta página. Cada comando pede motivo, a palavra de confirmação e a sua senha, e fica na auditoria. Há um intervalo mínimo entre comandos no mesmo
        programa.
      </p>
    </div>
  );
}

export default function ServerPage() {
  return (
    <>
      <PageTitle title="Ligar / reiniciar">Liga, desliga e reinicia os programas do servidor do jogo na VPS.</PageTitle>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Body />
      </Suspense>
    </>
  );
}
