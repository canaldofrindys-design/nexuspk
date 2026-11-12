"use client";

import { useActionState } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { initialFormState } from "@/features/auth/types";
import { eventCategories, newsCategories } from "@/features/content/labels";
import type { FormState } from "@/features/auth/types";
import { safeImage } from "@/features/content/labels";
import type { BannerRecord, DownloadSettings, ServerSettings } from "@/features/site/types";
import {
  createStaffAction,
  resetStaffPasswordAction,
  saveBannerAction,
  saveDownloadsAction,
  saveEventAction,
  saveNewsAction,
  saveServerSettingsAction,
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
      </div>
      <ImageField current={v("imageUrl")} error={e.imageUrl} />
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
      </div>
      <ImageField current={v("imageUrl")} error={e.imageUrl} />
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

/**
 * Imagem de notícia, evento ou banner: enviar um arquivo (PNG ou JPG até 3 MB, os metadados são removidos)
 * ou informar um endereço. O arquivo enviado tem prioridade sobre o endereço.
 */
function ImageField({ current, error }: { current: string; error?: string }) {
  const preview = safeImage(current || null);
  return (
    <fieldset className="grid gap-3 border border-steel/60 p-3 md:grid-cols-[1fr_1fr_auto] md:items-start">
      <legend className="px-1 text-sm text-silver">Imagem (opcional)</legend>
      <div className="flex flex-col gap-1">
        <label htmlFor="f-imageFile" className="text-sm text-silver/80">
          Enviar do computador
        </label>
        <input
          id="f-imageFile"
          name="imageFile"
          type="file"
          accept="image/png,image/jpeg"
          aria-describedby="f-imageFile-hint"
          className="text-sm text-silver/80 file:mr-3 file:rounded-sm file:border file:border-bronze/70 file:bg-night file:px-3 file:py-1.5 file:text-silver hover:file:border-gold"
        />
        <p id="f-imageFile-hint" className="text-xs text-silver/60">
          PNG ou JPG até 3 MB. Substitui o endereço ao lado.
        </p>
      </div>
      <Field label="Ou endereço da imagem" name="imageUrl" maxLength={400} defaultValue={current} error={error} hint="Caminho do site (/midia/...) ou https. Vazio tira a imagem." />
      {preview ? (
        // eslint-disable-next-line @next/next/no-img-element -- prévia da imagem atual
        <img src={preview} alt="Imagem atual" className="h-20 w-32 border border-steel/60 object-cover" />
      ) : null}
    </fieldset>
  );
}

/** Botão de ação sensível (excluir, encerrar) que pede confirmação antes de enviar. */
export function ConfirmActionForm({
  action,
  id,
  label,
  confirm,
  tone = "danger",
}: {
  action: (state: FormState, form: FormData) => Promise<FormState>;
  id: number;
  label: string;
  confirm: string;
  tone?: "danger" | "neutral";
}) {
  const [state, run, pending] = useActionState(action, initialFormState);
  const color = tone === "danger" ? "border-data-bad/70 text-data-bad hover:bg-data-bad/10" : "border-arcane/70 text-arcane hover:bg-arcane/10";
  return (
    <form
      action={run}
      onSubmit={(ev) => {
        if (!window.confirm(confirm)) ev.preventDefault();
      }}
      className="flex flex-col gap-1"
    >
      <input type="hidden" name="id" value={id} />
      <button type="submit" disabled={pending} className={`border px-3 py-1.5 text-sm disabled:opacity-60 ${color}`}>
        {pending ? "Aguarde…" : label}
      </button>
      {state.status === "error" ? <p className="max-w-xs text-xs text-data-bad">{state.message}</p> : null}
    </form>
  );
}

export function BannerForm({ banner }: { banner?: BannerRecord }) {
  const [state, action] = useActionState(saveBannerAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const v = (k: keyof BannerRecord, fallback = "") => state.values?.[k] ?? (banner?.[k] != null ? String(banner[k]) : fallback);
  const active = state.values ? state.values.active === "true" : (banner?.active ?? true);
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      {banner ? <input type="hidden" name="id" value={banner.id} /> : null}
      <FormMessage state={state} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Título" name="title" required maxLength={120} defaultValue={v("title")} error={e.title} />
        <Field label="Subtítulo (opcional)" name="subtitle" maxLength={300} defaultValue={v("subtitle")} error={e.subtitle} />
        <Field label="Link do botão (opcional)" name="linkUrl" maxLength={400} defaultValue={v("linkUrl")} error={e.linkUrl} hint="Página do site (/eventos/...) ou endereço https." />
        <Field label="Texto do botão" name="linkLabel" maxLength={40} defaultValue={v("linkLabel")} error={e.linkLabel} hint='Vazio usa "Saiba mais".' />
        <Field label="Ordem" name="sortOrder" type="number" min={0} max={999} defaultValue={v("sortOrder", "0")} error={e.sortOrder} hint="Menor aparece primeiro." />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Mostrar a partir de" name="startsLocal" type="datetime-local" defaultValue={state.values?.startsLocal ?? toLocalInput(banner?.startsUtc)} error={e.startsLocal} />
          <Field label="Mostrar até" name="endsLocal" type="datetime-local" defaultValue={state.values?.endsLocal ?? toLocalInput(banner?.endsUtc)} error={e.endsLocal} />
        </div>
      </div>
      <ImageField current={v("imageUrl")} error={e.imageUrl} />
      <label className="flex items-center gap-2 text-sm text-silver">
        <input key={String(active)} type="checkbox" name="active" defaultChecked={active} /> Ativo (aparece na página inicial)
      </label>
      <p className="text-xs text-silver/60">A home mostra até 5 banners ativos, girando a cada 7 segundos. Horários de Brasília.</p>
      <div className="max-w-xs">
        <SubmitButton>{banner ? "Salvar alterações" : "Criar banner"}</SubmitButton>
      </div>
    </form>
  );
}

function slots(filled: number, min: number, max: number) {
  return Array.from({ length: Math.min(max, Math.max(min, filled + 2)) }, (_, i) => i);
}

export function ServerSettingsForm({ settings }: { settings: ServerSettings }) {
  const [state, action] = useActionState(saveServerSettingsAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const val = (k: string, fallback: string) => state.values?.[k] ?? fallback;
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <FormMessage state={state} />
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Nome do servidor" name="name" required maxLength={40} defaultValue={val("name", settings.name)} error={e.name} />
        <Field label="Slogan (opcional)" name="tagline" maxLength={200} defaultValue={val("tagline", settings.tagline)} error={e.tagline} hint="Aparece na abertura da home." />
      </div>
      <TextArea
        label='Texto da página "O Servidor"'
        name="about"
        rows={10}
        maxLength={20000}
        defaultValue={val("about", settings.about)}
        error={e.about}
        hint={formatHint}
      />
      <fieldset className="flex flex-col gap-2 border border-steel/60 p-3">
        <legend className="px-1 text-sm text-silver">Taxas</legend>
        {slots(settings.rates.length, 4, 12).map((i) => (
          <div key={i} className="grid grid-cols-2 gap-2">
            <input aria-label={`Taxa ${i + 1}: nome`} name={`rateLabel_${i}`} maxLength={30} placeholder="Ex.: EXP" defaultValue={val(`rateLabel_${i}`, settings.rates[i]?.label ?? "")} className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver" />
            <input aria-label={`Taxa ${i + 1}: valor`} name={`rateValue_${i}`} maxLength={30} placeholder="Ex.: 10x" defaultValue={val(`rateValue_${i}`, settings.rates[i]?.value ?? "")} className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver" />
          </div>
        ))}
        {e.rates ? <p className="text-xs text-data-bad">{e.rates}</p> : <p className="text-xs text-silver/60">Linha vazia é ignorada. Salve para ganhar mais linhas (até 12).</p>}
      </fieldset>
      <fieldset className="flex flex-col gap-2 border border-steel/60 p-3">
        <legend className="px-1 text-sm text-silver">Comunidade e redes sociais</legend>
        {slots(settings.communityLinks.length, 3, 8).map((i) => (
          <div key={i} className="grid grid-cols-[minmax(0,1fr)_minmax(0,2fr)] gap-2">
            <input aria-label={`Rede ${i + 1}: nome`} name={`linkLabel_${i}`} maxLength={30} placeholder="Ex.: Discord" defaultValue={val(`linkLabel_${i}`, settings.communityLinks[i]?.label ?? "")} className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver" />
            <input aria-label={`Rede ${i + 1}: endereço`} name={`linkUrl_${i}`} type="url" maxLength={400} placeholder="https://discord.gg/..." defaultValue={val(`linkUrl_${i}`, settings.communityLinks[i]?.url ?? "")} className="rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver" />
          </div>
        ))}
        {e.communityLinks ? <p className="text-xs text-data-bad">{e.communityLinks}</p> : <p className="text-xs text-silver/60">Aparecem no rodapé, na home e na página O Servidor. Só endereços https.</p>}
      </fieldset>
      <div className="max-w-xs">
        <SubmitButton>Salvar informações do servidor</SubmitButton>
      </div>
    </form>
  );
}

export function DownloadsForm({ settings }: { settings: DownloadSettings }) {
  const [state, action] = useActionState(saveDownloadsAction, initialFormState);
  const e = state.fieldErrors ?? {};
  const val = (k: string, fallback: string | null | undefined) => state.values?.[k] ?? fallback ?? "";
  const input = "rounded-sm border border-bronze/70 bg-night/80 px-3 py-1.5 text-sm text-silver";
  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <FormMessage state={state} />
      <TextArea label="Texto de abertura (opcional)" name="intro" rows={4} maxLength={4000} defaultValue={val("intro", settings.intro)} error={e.intro} hint={formatHint} />
      <TextArea label="Requisitos (opcional)" name="requirements" rows={4} maxLength={4000} defaultValue={val("requirements", settings.requirements)} error={e.requirements} hint='Ex.: "- Windows 10 ou 11" em cada linha.' />
      <fieldset className="flex flex-col gap-3 border border-steel/60 p-3">
        <legend className="px-1 text-sm text-silver">Arquivos</legend>
        {slots(settings.items.length, 3, 10).map((i) => {
          const d = settings.items[i];
          return (
            <div key={i} className="grid gap-2 border-b border-steel/40 pb-3 last:border-0 md:grid-cols-[1.2fr_2fr]">
              <input aria-label={`Arquivo ${i + 1}: nome`} name={`dlName_${i}`} maxLength={80} placeholder="Ex.: Cliente completo" defaultValue={val(`dlName_${i}`, d?.name)} className={input} />
              <input aria-label={`Arquivo ${i + 1}: link`} name={`dlUrl_${i}`} maxLength={400} placeholder="https://... (Google Drive, Mega, servidor próprio)" defaultValue={val(`dlUrl_${i}`, d?.url)} className={input} />
              <input aria-label={`Arquivo ${i + 1}: descrição`} name={`dlDescription_${i}`} maxLength={300} placeholder="Descrição (opcional)" defaultValue={val(`dlDescription_${i}`, d?.description)} className={`${input} md:col-span-2`} />
              <div className="grid grid-cols-2 gap-2 md:col-span-2 md:max-w-sm">
                <input aria-label={`Arquivo ${i + 1}: tamanho`} name={`dlSize_${i}`} maxLength={20} placeholder="Tamanho (ex.: 1,2 GB)" defaultValue={val(`dlSize_${i}`, d?.size)} className={input} />
                <input aria-label={`Arquivo ${i + 1}: versão`} name={`dlVersion_${i}`} maxLength={30} placeholder="Versão (ex.: v1.0)" defaultValue={val(`dlVersion_${i}`, d?.version)} className={input} />
              </div>
            </div>
          );
        })}
        {e.items ? <p className="text-xs text-data-bad">{e.items}</p> : <p className="text-xs text-silver/60">Linha vazia é ignorada. Salve para ganhar mais linhas (até 10).</p>}
      </fieldset>
      <div className="max-w-xs">
        <SubmitButton>Salvar página de download</SubmitButton>
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
