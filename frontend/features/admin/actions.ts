"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { apiSend, type ApiResult } from "@/services/api";
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
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/news/${existing}` : "/api/v1/admin/news", {
    token: await token(),
    body,
    ...(await clientContext()),
  });
  const values = Object.fromEntries(Object.entries(body).map(([k, v]) => [k, String(v)]));
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
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/events/${existing}` : "/api/v1/admin/events", {
    token: await token(),
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
