"use client";

import { useActionState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { initialFormState } from "@/features/auth/types";
import { formatDateTime } from "@/features/home/format";
import { disconnectGameAction } from "./actions";
import type { DisconnectRequest, DisconnectState } from "./types";

const statusText: Record<DisconnectRequest["status"], { text: string; tone: string }> = {
  pending: { text: "aguardando o servidor do jogo…", tone: "text-data-info" },
  done: { text: "conta desconectada. Já pode entrar no jogo.", tone: "text-data-ok" },
  "not-online": { text: "a conta não estava presa no servidor. Já pode entrar no jogo.", tone: "text-data-ok" },
  failed: { text: "o servidor não conseguiu desconectar. Abra um chamado no suporte.", tone: "text-data-bad" },
  expired: { text: "o servidor não respondeu a tempo. Tente de novo.", tone: "text-data-bad" },
};

/** Enquanto o pedido está pendente, recarrega a janela a cada 4 s para mostrar a resposta do servidor. */
function useRefreshWhilePending(pending: boolean) {
  const router = useRouter();
  useEffect(() => {
    if (!pending) return;
    let ticks = 0;
    const id = window.setInterval(() => {
      ticks += 1;
      router.refresh();
      if (ticks >= 40) window.clearInterval(id);
    }, 4000);
    return () => window.clearInterval(id);
  }, [pending, router]);
}

export function DisconnectForm({ state }: { state: DisconnectState }) {
  const [form, action] = useActionState(disconnectGameAction, initialFormState);
  const last = state.last;
  useRefreshWhilePending(last?.status === "pending");
  const label = last ? statusText[last.status] : null;
  return (
    <div className="flex flex-col gap-3">
      {last && label ? (
        <p className="text-sm text-silver/80" role="status">
          Último pedido em <span className="tabular">{formatDateTime(last.requestedUtc)}</span>: <span className={label.tone}>{label.text}</span>
        </p>
      ) : null}
      {form.status === "error" ? <FormMessage state={form} /> : null}
      {state.available ? (
        <form action={action}>
          <SubmitButton>Desconectar minha conta do jogo</SubmitButton>
        </form>
      ) : last?.status !== "pending" && state.unavailableReason ? (
        <p className="text-sm text-silver/70">{state.unavailableReason}</p>
      ) : null}
    </div>
  );
}
