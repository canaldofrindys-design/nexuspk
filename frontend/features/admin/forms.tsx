"use client";

import { useActionState } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { initialFormState } from "@/features/auth/types";
import { eventCategories, newsCategories } from "@/features/content/labels";
import {
  createStaffAction,
  resetStaffPasswordAction,
  saveEventAction,
  saveNewsAction,
  staffLoginAction,
  staffPasswordAction,
  updateStaffAction,
} from "./actions";
import { toLocalInput } from "./dates";
import { Select, TextArea } from "./inputs";
import { eventStatusLabels, newsStatusLabels, roleLabels, type EventRecord, type NewsRecord, type StaffItem } from "./types";

const formatHint = "Texto simples: linha em branco separa parágrafos, \"## \" cria subtítulo e \"- \" cria lista. HTML aparece como texto.";

export function StaffLoginForm() {
  const [state, action] = useActionState(staffLoginAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <Field label="Usuário" name="username" autoComplete="username" required maxLength={32} defaultValue={state.values?.username} />
      <Field label="Senha" name="password" type="password" autoComplete="current-password" required maxLength={128} />
      <SubmitButton>Entrar no painel</SubmitButton>
    </form>
  );
}

export function StaffPasswordForm({ forced }: { forced: boolean }) {
  const [state, action] = useActionState(staffPasswordAction, initialFormState);
  const e = state.fieldErrors ?? {};
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      {forced ? <p className="text-sm text-data-info">Sua senha é temporária. Defina uma senha nova para usar o painel.</p> : null}
      <FormMessage state={state} />
      <Field label="Senha atual" name="currentPassword" type="password" autoComplete="current-password" required maxLength={128} error={e.currentPassword} />
      <Field label="Nova senha" name="newPassword" type="password" autoComplete="new-password" required minLength={12} maxLength={128} error={e.newPassword} hint="Pelo menos 12 caracteres, sem conter o seu usuário." />
      <Field label="Repita a nova senha" name="newPasswordConfirm" type="password" autoComplete="new-password" required maxLength={128} error={e.newPasswordConfirm} />
      <SubmitButton>Trocar senha</SubmitButton>
    </form>
  );
}

const newsCategoryOptions = Object.fromEntries(Object.entries(newsCategories).map(([k, v]) => [k, v.label]));

export function NewsForm({ news }: { news?: NewsRecord }) {
  const [state, action] = useActionState(saveNewsAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const v = (k: keyof NewsRecord, fallback = "") => state.values?.[k] ?? (news?.[k] != null ? String(news[k]) : fallback);
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      {news ? <input type="hidden" name="id" value={news.id} /> : null}
      <FormMessage state={state} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Título" name="title" required maxLength={160} defaultValue={v("title")} error={e.title} />
        <Field label="Endereço" name="slug" required maxLength={120} defaultValue={v("slug")} error={e.slug} hint="Ex.: manutencao-de-sabado. Fica em /noticias/endereco." />
        <Select label="Categoria" name="category" options={newsCategoryOptions} defaultValue={v("category", "noticia")} error={e.category} />
        <Select label="Situação" name="status" options={newsStatusLabels} defaultValue={v("status", "draft")} error={e.status} />
        <Field
          label="Publicar em (horário de Brasília)"
          name="publishedLocal"
          type="datetime-local"
          defaultValue={state.values?.publishedLocal ?? toLocalInput(news?.publishedUtc)}
          error={e.publishedLocal}
          hint="Vazio publica agora. Data futura agenda a publicação."
        />
        <Field label="Imagem (opcional)" name="imageUrl" maxLength={400} defaultValue={v("imageUrl")} error={e.imageUrl} hint="Caminho do site (/imagens/...) ou endereço https." />
      </div>
      <TextArea label="Resumo" name="summary" required maxLength={400} rows={2} defaultValue={v("summary")} error={e.summary} />
      <TextArea label="Texto" name="body" required rows={14} defaultValue={v("body")} error={e.body} hint={formatHint} />
      <label className="flex items-center gap-2 text-sm text-silver">
        <input key={String(state.values?.pinned ?? news?.pinned)} type="checkbox" name="pinned" defaultChecked={state.values ? state.values.pinned === "true" : news?.pinned} /> Fixar no topo da lista
      </label>
      <div className="max-w-xs">
        <SubmitButton>{news ? "Salvar alterações" : "Criar notícia"}</SubmitButton>
      </div>
    </form>
  );
}

export function EventForm({ event }: { event?: EventRecord }) {
  const [state, action] = useActionState(saveEventAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const v = (k: keyof EventRecord, fallback = "") => state.values?.[k] ?? (event?.[k] != null ? String(event[k]) : fallback);
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      {event ? <input type="hidden" name="id" value={event.id} /> : null}
      <FormMessage state={state} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Nome" name="name" required maxLength={120} defaultValue={v("name")} error={e.name} />
        <Field label="Endereço" name="slug" required maxLength={120} defaultValue={v("slug")} error={e.slug} hint="Ex.: boss-de-sabado. Fica em /eventos/endereco." />
        <Select label="Tipo" name="category" options={eventCategories} defaultValue={v("category", "exp")} error={e.category} />
        <Select label="Situação" name="status" options={eventStatusLabels} defaultValue={v("status", "draft")} error={e.status} />
        <Field label="Início (horário de Brasília)" name="startsLocal" type="datetime-local" required defaultValue={state.values?.startsLocal ?? toLocalInput(event?.startsUtc)} error={e.startsLocal} />
        <Field
          label="Fim (opcional)"
          name="endsLocal"
          type="datetime-local"
          defaultValue={state.values?.endsLocal ?? toLocalInput(event?.endsUtc)}
          error={e.endsLocal}
          hint="Sem fim, o evento aparece como em andamento por 1 hora."
        />
        <Field label="Local (opcional)" name="location" maxLength={120} defaultValue={v("location")} error={e.location} />
        <Field label="Imagem (opcional)" name="imageUrl" maxLength={400} defaultValue={v("imageUrl")} error={e.imageUrl} hint="Caminho do site (/imagens/...) ou endereço https." />
      </div>
      <TextArea label="Resumo" name="summary" required maxLength={400} rows={2} defaultValue={v("summary")} error={e.summary} />
      <TextArea label="Descrição" name="description" required rows={8} defaultValue={v("description")} error={e.description} hint={formatHint} />
      <TextArea label="Regulamento (opcional)" name="rules" rows={6} defaultValue={v("rules")} error={e.rules} />
      <TextArea label="Premiação (opcional)" name="prizes" rows={4} defaultValue={v("prizes")} error={e.prizes} />
      <p className="text-xs text-silver/60">
        Salvar o evento só publica a agenda no site. Nada é ligado no servidor do jogo: a equipe conduz o evento no horário.
      </p>
      <div className="max-w-xs">
        <SubmitButton>{event ? "Salvar alterações" : "Criar evento"}</SubmitButton>
      </div>
    </form>
  );
}

export function CreateStaffForm() {
  const [state, action] = useActionState(createStaffAction, initialFormState);
  const e = state.fieldErrors ?? {};
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <Field label="Usuário" name="username" required maxLength={32} autoComplete="off" defaultValue={state.status === "error" ? state.values?.username : ""} error={e.username} />
      <Field label="Nome de exibição" name="displayName" required maxLength={64} defaultValue={state.status === "error" ? state.values?.displayName : ""} error={e.displayName} />
      <Select label="Função" name="role" options={roleLabels} defaultValue={state.values?.role ?? "suporte"} error={e.role} />
      <Field label="Senha temporária" name="temporaryPassword" type="password" autoComplete="new-password" required minLength={12} maxLength={128} error={e.temporaryPassword} hint="O membro troca no primeiro acesso." />
      <SubmitButton variant="arcane">Adicionar à equipe</SubmitButton>
    </form>
  );
}

export function EditStaffForm({ member, self }: { member: StaffItem; self: boolean }) {
  const [state, action] = useActionState(updateStaffAction, initialFormState);
  const [pwState, pwAction] = useActionState(resetStaffPasswordAction, initialFormState);
  return (
    <div className="flex flex-col gap-3">
      <form action={action} className="flex flex-col gap-2" noValidate>
        <input type="hidden" name="id" value={member.id} />
        <FormMessage state={state} />
        <Field label="Nome de exibição" name="displayName" id={`dn-${member.id}`} maxLength={64} defaultValue={member.displayName} error={state.fieldErrors?.displayName} />
        <Select label="Função" name="role" id={`role-${member.id}`} options={roleLabels} defaultValue={member.role} disabled={self} error={state.fieldErrors?.role} />
        {self ? <input type="hidden" name="role" value={member.role} /> : null}
        <label className="flex items-center gap-2 text-sm text-silver">
          <input type="checkbox" name="active" defaultChecked={member.active} disabled={self} /> Ativo
        </label>
        {self ? <input type="hidden" name="active" value="on" /> : null}
        <SubmitButton variant="arcane">Salvar</SubmitButton>
      </form>
      {!self ? (
        <form action={pwAction} className="flex flex-col gap-2 border-t border-steel/60 pt-3" noValidate>
          <input type="hidden" name="id" value={member.id} />
          <FormMessage state={pwState} />
          <Field label="Nova senha temporária" name="temporaryPassword" id={`tp-${member.id}`} type="password" autoComplete="new-password" minLength={12} maxLength={128} error={pwState.fieldErrors?.temporaryPassword} />
          <SubmitButton variant="arcane">Definir senha temporária</SubmitButton>
        </form>
      ) : null}
    </div>
  );
}
