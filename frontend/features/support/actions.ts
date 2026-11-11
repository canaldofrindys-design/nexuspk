"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { clientContext, getSessionToken } from "@/features/auth/session";
import type { FormState } from "@/features/auth/types";
import { getStaffToken } from "@/features/admin/session";
import { apiSend, apiSendForm, type ApiResult } from "@/services/api";
import { MAX_FILE_BYTES, MAX_FILES } from "./types";

function text(form: FormData, name: string): string {
  const v = form.get(name);
  return typeof v === "string" ? v : "";
}

function positiveId(form: FormData, name = "id"): number | null {
  const n = Number(text(form, name));
  return Number.isInteger(n) && n > 0 ? n : null;
}

/** Copia só os campos esperados e as imagens não vazias, conferindo quantidade e tamanho antes de enviar. */
function outgoing(form: FormData, fields: string[]): { data: FormData } | { error: string } {
  const data = new FormData();
  for (const f of fields) data.set(f, text(form, f));
  const files = form.getAll("files").filter((f): f is File => typeof f !== "string" && f.size > 0);
  if (files.length > MAX_FILES) return { error: `Envie no máximo ${MAX_FILES} imagens por mensagem.` };
  if (files.some((f) => f.size > MAX_FILE_BYTES)) return { error: "Cada imagem pode ter no máximo 3 MB." };
  for (const f of files) data.append("files", f, f.name);
  return { data };
}

async function playerToken(next: string): Promise<string> {
  const token = await getSessionToken();
  if (!token) redirect(`/login?next=${encodeURIComponent(next)}`);
  return token;
}

function playerFailure(r: Extract<ApiResult<unknown>, { ok: false }>, next: string, values?: Record<string, string>): FormState {
  if (r.status === 401) redirect(`/login?next=${encodeURIComponent(next)}`);
  const fieldErrors = r.error.errors ?? undefined;
  return { status: "error", message: fieldErrors ? "Confira os campos destacados." : r.error.message, fieldErrors, values };
}

export async function openTicketAction(_: FormState, form: FormData): Promise<FormState> {
  const next = "/painel/suporte/novo";
  const token = await playerToken(next);
  const values = { category: text(form, "category"), subject: text(form, "subject"), body: text(form, "body") };
  const out = outgoing(form, ["category", "subject", "body"]);
  if ("error" in out) return { status: "error", message: "Confira os campos destacados.", fieldErrors: { files: out.error }, values };
  const r = await apiSendForm<{ id: number }>("/api/v1/player/tickets", out.data, { token, ...(await clientContext()) });
  if (!r.ok) return playerFailure(r, next, values);
  revalidatePath("/painel/suporte");
  redirect(`/painel/suporte/${r.data.id}?aberto=1`);
}

export async function playerReplyAction(_: FormState, form: FormData): Promise<FormState> {
  const id = positiveId(form);
  if (!id) return { status: "error", message: "Chamado inválido." };
  const next = `/painel/suporte/${id}`;
  const token = await playerToken(next);
  const out = outgoing(form, ["body"]);
  if ("error" in out) return { status: "error", message: "Confira os campos destacados.", fieldErrors: { files: out.error }, values: { body: text(form, "body") } };
  const r = await apiSendForm(`/api/v1/player/tickets/${id}/messages`, out.data, { token, ...(await clientContext()) });
  if (!r.ok) return playerFailure(r, next, { body: text(form, "body") });
  revalidatePath(next);
  return { status: "success", message: "Mensagem enviada. A equipe vai ver na fila de atendimento." };
}

export async function closeTicketAction(_: FormState, form: FormData): Promise<FormState> {
  const id = positiveId(form);
  if (!id) return { status: "error", message: "Chamado inválido." };
  const next = `/painel/suporte/${id}`;
  const token = await playerToken(next);
  const r = await apiSend("POST", `/api/v1/player/tickets/${id}/close`, { token, ...(await clientContext()) });
  if (!r.ok) return playerFailure(r, next);
  revalidatePath(next);
  return { status: "success", message: "Chamado encerrado." };
}

// ---------- Equipe ----------

async function staffToken(): Promise<string> {
  const t = await getStaffToken();
  if (!t) redirect("/admin/entrar");
  return t;
}

function staffFailure(r: Extract<ApiResult<unknown>, { ok: false }>, values?: Record<string, string>): FormState {
  if (r.status === 401) redirect("/admin/entrar");
  if (r.error.code === "must-change-password") redirect("/admin/senha");
  const fieldErrors = r.error.errors ?? undefined;
  return { status: "error", message: fieldErrors ? "Confira os campos destacados." : r.error.message, fieldErrors, values };
}

export async function staffReplyAction(_: FormState, form: FormData): Promise<FormState> {
  const id = positiveId(form);
  if (!id) return { status: "error", message: "Chamado inválido." };
  const token = await staffToken();
  const isInternal = form.get("internal") === "on";
  const values = { body: text(form, "body"), statusAfter: text(form, "statusAfter"), internal: isInternal ? "on" : "" };
  const out = outgoing(form, ["body", "statusAfter"]);
  if ("error" in out) return { status: "error", message: "Confira os campos destacados.", fieldErrors: { files: out.error }, values };
  out.data.set("internal", isInternal ? "true" : "false");
  const r = await apiSendForm(`/api/v1/admin/tickets/${id}/messages`, out.data, { token, ...(await clientContext()) });
  if (!r.ok) return staffFailure(r, values);
  revalidatePath(`/admin/chamados/${id}`);
  return { status: "success", message: isInternal ? "Nota interna salva. O jogador não vê esta nota." : "Resposta enviada ao jogador." };
}

export async function updateTicketAction(_: FormState, form: FormData): Promise<FormState> {
  const id = positiveId(form);
  if (!id) return { status: "error", message: "Chamado inválido." };
  const assigned = Number(text(form, "assignedStaffId"));
  const r = await apiSend("PUT", `/api/v1/admin/tickets/${id}`, {
    token: await staffToken(),
    body: { status: text(form, "status"), priority: text(form, "priority"), assignedStaffId: Number.isInteger(assigned) && assigned > 0 ? assigned : null },
    ...(await clientContext()),
  });
  if (!r.ok) return staffFailure(r);
  revalidatePath(`/admin/chamados/${id}`);
  return { status: "success", message: "Chamado atualizado." };
}

export async function saveNoticeAction(_: FormState, form: FormData): Promise<FormState> {
  const existing = positiveId(form);
  const body = {
    kind: text(form, "kind"),
    title: text(form, "title"),
    body: text(form, "body"),
    startsLocal: text(form, "startsLocal"),
    endsLocal: text(form, "endsLocal"),
    status: text(form, "status"),
  };
  const r = await apiSend<{ id: number }>(existing ? "PUT" : "POST", existing ? `/api/v1/admin/notices/${existing}` : "/api/v1/admin/notices", {
    token: await staffToken(),
    body,
    ...(await clientContext()),
  });
  if (!r.ok) return staffFailure(r, body);
  revalidatePath("/admin/avisos");
  revalidatePath("/status");
  redirect("/admin/avisos?salvo=1");
}
