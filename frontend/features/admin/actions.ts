"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { apiSend, apiSendForm, type ApiResult } from "@/services/api";
import { clientContext } from "@/features/auth/session";
import type { FormState } from "@/features/auth/types";
import { clearStaffCookie, getStaffToken, setStaffCookie } from "./session";

function text(form: FormData, name: string): string {
  const v = form.get(name);
  return typeof v === "string" ? v : "";
}

function id(form: FormData): number | null {
  const n = Number(text(form, "id"));
  return Number.isInteger(n) && n > 0 ? n : null;
}

async function token(): Promise<string> {
  const t = await getStaffToken();
  if (!t) redirect("/admin/entrar");
  return t;
}

function failure(r: Extract<ApiResult<unknown>, { ok: false }>, values?: Record<string, string>): FormState {
  if (r.status === 401) redirect("/admin/entrar");
  if (r.error.code === "must-change-password") redirect("/admin/senha");
  const fieldErrors = r.error.errors ?? undefined;
  return { status: "error", message: fieldErrors ? "Confira os campos destacados." : r.error.message, fieldErrors, values };
}

const MAX_IMAGE_BYTES = 3 * 1024 * 1024;

/** Envia a imagem escolhida no formulário (campo "imageFile") e devolve o endereço público dela. */
async function uploadImage(form: FormData, t: string): Promise<{ url?: string; error?: string }> {
  const file = form.get("imageFile");
  if (!(file instanceof File) || file.size === 0) return {};
  if (file.size > MAX_IMAGE_BYTES) return { error: "A imagem pode ter no máximo 3 MB." };
  const data = new FormData();
  data.append("file", file, file.name);
  const r = await apiSendForm<{ id: string; url: string }>("/api/v1/admin/media", data, { token: t, ...(await clientContext()) });
  if (r.ok) return { url: r.data.url };
  if (r.status === 401) redirect("/admin/entrar");
  return { error: r.error.errors?.file ?? r.error.message };
}

function imageError(error: string, values: Record<string, string>): FormState {
  return { status: "error", message: "Confira os campos destacados.", fieldErrors: { imageUrl: error }, values };
}

export async function staffLoginAction(_: FormState, form: FormData): Promise<FormState> {
  const username = text(form, "username").trim();
  const r = await apiSend<{ token: string; expiresUtc: string; mustChangePassword: boolean }>("POST", "/api/v1/admin/login", {
    body: { username, password: text(form, "password") },
    ...(await clientContext()),
  });
  if (!r.ok) return { status: "error", message: r.error.message, values: { username } };
  await setStaffCookie(r.data.token, r.data.expiresUtc);
  redirect(r.data.mustChangePassword ? "/admin/senha" : "/admin");
}

export async function staffLogoutAction(): Promise<void> {
  const t = await getStaffToken();
  if (t) await apiSend("POST", "/api/v1/admin/logout", { token: t });
  await clearStaffCookie();
  redirect("/admin/entrar");
}

export async function staffPasswordAction(_: FormState, form: FormData): Promise<FormState> {
  const newPassword = text(form, "newPassword");
  if (newPassword !== text(form, "newPasswordConfirm")) {
    return { status: "error", message: "Confira os campos destacados.", fieldErrors: { newPasswordConfirm: "As senhas não conferem." } };
  }
  const r = await apiSend("POST", "/api/v1/admin/password", {
    token: await token(),
    body: { currentPassword: text(form, "currentPassword"), newPassword },
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r);
  redirect("/admin?senha=1");
}

function newsBody(form: FormData) {
  return {
    slug: text(form, "slug"),
    category: text(form, "category"),
    title: text(form, "title"),
    summary: text(form, "summary"),
    body: text(form, "body"),
    imageUrl: text(form, "imageUrl"),
    pinned: form.get("pinned") === "on",
    status: text(form, "status"),
    publishedLocal: text(form, "publishedLocal"),
  };
}

export async function saveNewsAction(_: FormState, form: FormData): Promise<FormState> {
  const existing = id(form);
  const body = newsBody(form);
  const t = await token();
  const values = Object.fromEntries(Object.entries(body).map(([k, v]) => [k, String(v)]));
  const upload = await uploadImage(form, t);
  if (upload.error) return imageError(upload.error, values);
  if (upload.url) body.imageUrl = values.imageUrl = upload.url;
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/news/${existing}` : "/api/v1/admin/news", {
    token: t,
    body,
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r, values);
  revalidatePath("/admin/noticias");
  redirect(`/admin/noticias?salvo=${r.data.id}`);
}

function eventBody(form: FormData) {
  return {
    slug: text(form, "slug"),
    category: text(form, "category"),
    name: text(form, "name"),
    summary: text(form, "summary"),
    description: text(form, "description"),
    rules: text(form, "rules"),
    prizes: text(form, "prizes"),
    location: text(form, "location"),
    imageUrl: text(form, "imageUrl"),
    startsLocal: text(form, "startsLocal"),
    endsLocal: text(form, "endsLocal"),
    status: text(form, "status"),
  };
}

export async function saveEventAction(_: FormState, form: FormData): Promise<FormState> {
  const existing = id(form);
  const body = eventBody(form);
  const t = await token();
  const upload = await uploadImage(form, t);
  if (upload.error) return imageError(upload.error, body);
  if (upload.url) body.imageUrl = upload.url;
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/events/${existing}` : "/api/v1/admin/events", {
    token: t,
    body,
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r, body);
  revalidatePath("/admin/eventos");
  redirect(`/admin/eventos?salvo=${r.data.id}`);
}

export async function createStaffAction(_: FormState, form: FormData): Promise<FormState> {
  const body = {
    username: text(form, "username").trim(),
    displayName: text(form, "displayName"),
    role: text(form, "role"),
    temporaryPassword: text(form, "temporaryPassword"),
  };
  const r = await apiSend("POST", "/api/v1/admin/staff", { token: await token(), body, ...(await clientContext()) });
  if (!r.ok) return failure(r, { username: body.username, displayName: body.displayName, role: body.role });
  revalidatePath("/admin/equipe");
  return { status: "success", message: `Membro ${body.username} criado. Passe a senha temporária pessoalmente; ela será trocada no primeiro acesso.` };
}

export async function updateStaffAction(_: FormState, form: FormData): Promise<FormState> {
  const staffId = id(form);
  if (!staffId) return { status: "error", message: "Membro inválido." };
  const r = await apiSend("PUT", `/api/v1/admin/staff/${staffId}`, {
    token: await token(),
    body: { displayName: text(form, "displayName"), role: text(form, "role"), active: form.get("active") === "on" },
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r);
  revalidatePath("/admin/equipe");
  return { status: "success", message: "Alterações salvas." };
}

export async function resetStaffPasswordAction(_: FormState, form: FormData): Promise<FormState> {
  const staffId = id(form);
  if (!staffId) return { status: "error", message: "Membro inválido." };
  const r = await apiSend("POST", `/api/v1/admin/staff/${staffId}/password`, {
    token: await token(),
    body: { temporaryPassword: text(form, "temporaryPassword") },
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r);
  revalidatePath("/admin/equipe");
  return { status: "success", message: "Senha temporária definida. As sessões desse membro foram encerradas." };
}

// ---------- Excluir e encerrar ----------

async function removeOrAct(method: "DELETE" | "POST", path: string): Promise<FormState | null> {
  const r = await apiSend(method, path, { token: await token(), ...(await clientContext()) });
  if (r.ok) return null;
  if (r.status === 404) return { status: "error", message: "Não encontrado. Talvez já tenha sido excluído." };
  const f = failure(r);
  const first = f.fieldErrors ? Object.values(f.fieldErrors)[0] : undefined;
  return { status: "error", message: first ?? f.message };
}

export async function deleteNewsAction(_: FormState, form: FormData): Promise<FormState> {
  const newsId = id(form);
  if (!newsId) return { status: "error", message: "Notícia inválida." };
  const err = await removeOrAct("DELETE", `/api/v1/admin/news/${newsId}`);
  if (err) return err;
  revalidatePath("/admin/noticias");
  redirect("/admin/noticias?excluido=1");
}

export async function deleteEventAction(_: FormState, form: FormData): Promise<FormState> {
  const eventId = id(form);
  if (!eventId) return { status: "error", message: "Evento inválido." };
  const err = await removeOrAct("DELETE", `/api/v1/admin/events/${eventId}`);
  if (err) return err;
  revalidatePath("/admin/eventos");
  redirect("/admin/eventos?excluido=1");
}

export async function endEventAction(_: FormState, form: FormData): Promise<FormState> {
  const eventId = id(form);
  if (!eventId) return { status: "error", message: "Evento inválido." };
  const err = await removeOrAct("POST", `/api/v1/admin/events/${eventId}/end`);
  if (err) return err;
  revalidatePath(`/admin/eventos/${eventId}`);
  redirect(`/admin/eventos/${eventId}?encerrado=1`);
}

export async function deleteBannerAction(_: FormState, form: FormData): Promise<FormState> {
  const bannerId = id(form);
  if (!bannerId) return { status: "error", message: "Banner inválido." };
  const err = await removeOrAct("DELETE", `/api/v1/admin/banners/${bannerId}`);
  if (err) return err;
  revalidatePath("/admin/banners");
  redirect("/admin/banners?excluido=1");
}

// ---------- Banners ----------

export async function saveBannerAction(_: FormState, form: FormData): Promise<FormState> {
  const existing = id(form);
  const values: Record<string, string> = {
    title: text(form, "title"),
    subtitle: text(form, "subtitle"),
    imageUrl: text(form, "imageUrl"),
    linkUrl: text(form, "linkUrl"),
    linkLabel: text(form, "linkLabel"),
    sortOrder: text(form, "sortOrder"),
    active: form.get("active") === "on" ? "true" : "false",
    startsLocal: text(form, "startsLocal"),
    endsLocal: text(form, "endsLocal"),
  };
  const t = await token();
  const upload = await uploadImage(form, t);
  if (upload.error) return imageError(upload.error, values);
  if (upload.url) values.imageUrl = upload.url;
  const order = Number.parseInt(values.sortOrder || "0", 10);
  const body = { ...values, sortOrder: Number.isFinite(order) ? order : -1, active: values.active === "true" };
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/banners/${existing}` : "/api/v1/admin/banners", {
    token: t,
    body,
    ...(await clientContext()),
  });
  if (!r.ok) return failure(r, values);
  revalidatePath("/admin/banners");
  redirect(`/admin/banners?salvo=${r.data.id}`);
}

// ---------- Conteúdo do site ----------

/** Linhas repetidas do formulário (prefixo_0, prefixo_1…) viram uma lista; linhas vazias a API ignora. */
function rows<K extends string>(form: FormData, keys: Record<K, string>, max: number): Record<K, string>[] {
  const out: Record<K, string>[] = [];
  for (let i = 0; i < max; i++) {
    const row = Object.fromEntries(Object.entries(keys).map(([k, field]) => [k, text(form, `${field}_${i}`)])) as Record<K, string>;
    if (Object.values(row).some((v) => (v as string).trim() !== "")) out.push(row);
  }
  return out;
}

function formValues(form: FormData): Record<string, string> {
  const v: Record<string, string> = {};
  for (const [k, val] of form.entries()) if (typeof val === "string" && !k.startsWith("$")) v[k] = val;
  return v;
}

export async function saveServerSettingsAction(_: FormState, form: FormData): Promise<FormState> {
  const body = {
    name: text(form, "name"),
    tagline: text(form, "tagline"),
    about: text(form, "about"),
    rates: rows(form, { label: "rateLabel", value: "rateValue" }, 12),
    communityLinks: rows(form, { label: "linkLabel", url: "linkUrl" }, 8),
  };
  const r = await apiSend("PUT", "/api/v1/admin/site/server", { token: await token(), body, ...(await clientContext()) });
  if (!r.ok) return failure(r, formValues(form));
  revalidatePath("/admin/site");
  revalidatePath("/servidor");
  return { status: "success", message: "Informações do servidor salvas. O site mostra a mudança em até 1 minuto." };
}

export async function saveDownloadsAction(_: FormState, form: FormData): Promise<FormState> {
  const body = {
    intro: text(form, "intro"),
    requirements: text(form, "requirements"),
    items: rows(form, { name: "dlName", description: "dlDescription", url: "dlUrl", size: "dlSize", version: "dlVersion" }, 10),
  };
  const r = await apiSend("PUT", "/api/v1/admin/site/downloads", { token: await token(), body, ...(await clientContext()) });
  if (!r.ok) return failure(r, formValues(form));
  revalidatePath("/admin/site");
  revalidatePath("/download");
  return { status: "success", message: "Página de download salva. O site mostra a mudança em até 1 minuto." };
}
