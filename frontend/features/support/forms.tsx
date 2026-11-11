"use client";

import { useActionState } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { toLocalInput } from "@/features/admin/dates";
import { Select, TextArea } from "@/features/admin/inputs";
import { initialFormState } from "@/features/auth/types";
import { closeTicketAction, openTicketAction, playerReplyAction, saveNoticeAction, staffReplyAction, updateTicketAction } from "./actions";
import { ACCEPT, MAX_FILES, noticeKinds, noticeStatuses, ticketCategories, ticketPriorities, ticketStatuses, type ServerNotice, type SupportAgent, type TicketSummary } from "./types";

function FileField({ error }: { error?: string }) {
  const id = "f-files";
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm text-silver">
        Imagens (opcional)
      </label>
      <input
        id={id}
        name="files"
        type="file"
        accept={ACCEPT}
        multiple
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-err` : `${id}-hint`}
        className="text-sm text-silver/80 file:mr-3 file:rounded-sm file:border file:border-bronze/70 file:bg-night file:px-3 file:py-1.5 file:text-silver hover:file:border-gold"
      />
      {error ? (
        <p id={`${id}-err`} className="text-xs text-data-bad">
          {error} Selecione as imagens de novo.
        </p>
      ) : (
        <p id={`${id}-hint`} className="text-xs text-silver/60">
          Até {MAX_FILES} imagens PNG, JPG ou BMP, de até 3 MB cada. Dados de localização e de câmera são apagados ao enviar.
        </p>
      )}
    </div>
  );
}

export function NewTicketForm() {
  const [state, action] = useActionState(openTicketAction, initialFormState);
  const e = state.fieldErrors ?? {};
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <FormMessage state={state} />
      <Select label="Categoria" name="category" options={{ "": "Escolha…", ...ticketCategories }} defaultValue={state.values?.category ?? ""} error={e.category} />
      <Field label="Assunto" name="subject" required minLength={5} maxLength={120} defaultValue={state.values?.subject} error={e.subject} hint="Ex.: Item sumiu depois da troca com outro jogador" />
      <TextArea
        label="Descreva o problema"
        name="body"
        required
        minLength={20}
        maxLength={4000}
        rows={8}
        defaultValue={state.values?.body}
        error={e.body}
        hint="Diga o personagem envolvido, o mapa, o dia e o horário aproximado. Nunca escreva a sua senha: a equipe não pede senha."
      />
      <FileField error={e.files} />
      <div className="max-w-xs">
        <SubmitButton>Abrir chamado</SubmitButton>
      </div>
    </form>
  );
}

export function PlayerReplyForm({ ticketId }: { ticketId: number }) {
  const [state, action] = useActionState(playerReplyAction, initialFormState);
  const e = state.fieldErrors ?? {};
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="id" value={ticketId} />
      <FormMessage state={state} />
      <TextArea label="Sua resposta" name="body" required maxLength={4000} rows={5} defaultValue={state.status === "error" ? state.values?.body : ""} error={e.body} />
      <FileField error={e.files} />
      <div className="max-w-xs">
        <SubmitButton>Enviar resposta</SubmitButton>
      </div>
    </form>
  );
}

export function CloseTicketForm({ ticketId }: { ticketId: number }) {
  const [state, action] = useActionState(closeTicketAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-2">
      <input type="hidden" name="id" value={ticketId} />
      <FormMessage state={state} />
      <p className="text-xs text-silver/60">Encerre quando o problema estiver resolvido. Um chamado encerrado não recebe novas mensagens.</p>
      <div className="max-w-xs">
        <SubmitButton variant="arcane">Encerrar chamado</SubmitButton>
      </div>
    </form>
  );
}

const afterReply: Record<string, string> = {
  "": "Aguardando jogador (padrão)",
  em_atendimento: "Em atendimento",
  resolvido: "Resolvido",
  fechado: "Fechado",
};

export function StaffReplyForm({ ticketId }: { ticketId: number }) {
  const [state, action] = useActionState(staffReplyAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const keep = state.status === "error";
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="id" value={ticketId} />
      <FormMessage state={state} />
      <TextArea label="Mensagem" name="body" required maxLength={4000} rows={6} defaultValue={keep ? state.values?.body : ""} error={e.body} />
      <div className="grid gap-3 md:grid-cols-2">
        <Select label="Situação depois de enviar" name="statusAfter" options={afterReply} defaultValue={keep ? (state.values?.statusAfter ?? "") : ""} error={e.statusAfter} />
        <label className="flex items-center gap-2 self-end pb-2 text-sm text-silver">
          <input key={keep ? state.values?.internal : "x"} type="checkbox" name="internal" defaultChecked={keep && state.values?.internal === "on"} className="size-4 accent-[#c9a227]" />
          Nota interna (o jogador não vê)
        </label>
      </div>
      <FileField error={e.files} />
      <p className="text-xs text-silver/60">Resposta ao jogador gera um e-mail sem o texto, só com o link do chamado, se o e-mail da conta estiver confirmado.</p>
      <div className="max-w-xs">
        <SubmitButton>Enviar</SubmitButton>
      </div>
    </form>
  );
}

export function TicketControlsForm({ ticket, agents }: { ticket: TicketSummary; agents: SupportAgent[] }) {
  const [state, action] = useActionState(updateTicketAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const agentOptions = { "": "Ninguém", ...Object.fromEntries(agents.map((a) => [String(a.id), a.displayName])) };
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="id" value={ticket.id} />
      <FormMessage state={state} />
      <Select label="Situação" name="status" options={ticketStatuses} defaultValue={ticket.status} error={e.status} />
      <Select label="Prioridade" name="priority" options={ticketPriorities} defaultValue={ticket.priority} error={e.priority} />
      <Select label="Atendente" name="assignedStaffId" options={agentOptions} defaultValue={ticket.assignedStaffId ? String(ticket.assignedStaffId) : ""} error={e.assignedStaffId} />
      <SubmitButton variant="arcane">Salvar</SubmitButton>
    </form>
  );
}

export function NoticeForm({ notice }: { notice?: ServerNotice }) {
  const [state, action] = useActionState(saveNoticeAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const v = (k: keyof ServerNotice, fallback = "") => state.values?.[k] ?? (notice?.[k] != null ? String(notice[k]) : fallback);
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      {notice ? <input type="hidden" name="id" value={notice.id} /> : null}
      <FormMessage state={state} />
      <div className="grid gap-4 md:grid-cols-2">
        <Select label="Tipo" name="kind" options={noticeKinds} defaultValue={v("kind", "manutencao")} error={e.kind} />
        <Select label="Situação" name="status" options={noticeStatuses} defaultValue={v("status", "draft")} error={e.status} />
        <Field label="Início (horário de Brasília)" name="startsLocal" type="datetime-local" required defaultValue={state.values?.startsLocal ?? toLocalInput(notice?.startsUtc)} error={e.startsLocal} />
        <Field
          label="Fim"
          name="endsLocal"
          type="datetime-local"
          defaultValue={state.values?.endsLocal ?? toLocalInput(notice?.endsUtc)}
          error={e.endsLocal}
          hint="Obrigatório para manutenção. Aviso sem fim fica no ar até ser cancelado."
        />
      </div>
      <Field label="Título" name="title" required maxLength={160} defaultValue={v("title")} error={e.title} />
      <TextArea label="Texto" name="body" required maxLength={2000} rows={5} defaultValue={v("body")} error={e.body} />
      <p className="text-xs text-silver/60">
        O aviso aparece na página de status. Publicar uma manutenção não desliga nada no servidor: a equipe faz a manutenção no horário.
      </p>
      <div className="max-w-xs">
        <SubmitButton>{notice ? "Salvar alterações" : "Criar aviso"}</SubmitButton>
      </div>
    </form>
  );
}
