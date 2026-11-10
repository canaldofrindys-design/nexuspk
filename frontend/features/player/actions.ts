"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { apiSend } from "@/services/api";
import { clientContext, getSessionToken } from "@/features/auth/session";
import type { FormState } from "@/features/auth/types";

function text(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === "string" ? value : "";
}

async function sessionOrLogin(): Promise<string> {
  const token = await getSessionToken();
  if (!token) redirect("/login?next=/painel");
  return token;
}

function failure(result: { status: number; error: { message: string; errors?: Record<string, string> | null } }): FormState {
  if (result.status === 401) redirect("/login?next=/painel");
  const fieldErrors = result.error.errors ?? undefined;
  return { status: "error", message: fieldErrors ? "Confira os campos destacados." : result.error.message, fieldErrors };
}

export async function changePasswordAction(_: FormState, form: FormData): Promise<FormState> {
  const token = await sessionOrLogin();
  const newPassword = text(form, "newPassword");
  if (newPassword !== text(form, "newPasswordConfirm")) {
    return { status: "error", message: "Confira os campos destacados.", fieldErrors: { newPasswordConfirm: "As senhas não conferem." } };
  }
  const result = await apiSend("POST", "/api/v1/player/password", {
    token,
    body: { currentPassword: text(form, "currentPassword"), newPassword },
    ...(await clientContext()),
  });
  if (!result.ok) return failure(result);
  revalidatePath("/painel");
  return { status: "success", message: "Senha alterada. Use a nova senha no jogo. As outras sessões do site foram encerradas." };
}

export async function changeEmailAction(_: FormState, form: FormData): Promise<FormState> {
  const token = await sessionOrLogin();
  const email = text(form, "email").trim();
  const result = await apiSend<{ confirmationEmailSent: boolean }>("POST", "/api/v1/player/email", {
    token,
    body: { email },
    ...(await clientContext()),
  });
  if (!result.ok) return { ...failure(result), values: { email } };
  revalidatePath("/painel");
  return {
    status: "success",
    message: result.data.confirmationEmailSent
      ? `Enviamos um link de confirmação para ${email}.`
      : "E-mail salvo. O envio de e-mails ainda não está ativo; a confirmação fica pendente.",
  };
}

export async function resendConfirmationAction(): Promise<FormState> {
  const token = await sessionOrLogin();
  const result = await apiSend("POST", "/api/v1/player/email/resend", { token, ...(await clientContext()) });
  if (!result.ok) return failure(result);
  return { status: "success", message: "Link de confirmação reenviado." };
}

export async function revokeOtherSessionsAction(): Promise<FormState> {
  const token = await sessionOrLogin();
  const result = await apiSend<{ revoked: number }>("POST", "/api/v1/player/sessions/revoke-others", { token, ...(await clientContext()) });
  if (!result.ok) return failure(result);
  const n = result.data.revoked;
  return { status: "success", message: n === 0 ? "Não havia outras sessões abertas." : `${n} ${n === 1 ? "sessão encerrada" : "sessões encerradas"}.` };
}
