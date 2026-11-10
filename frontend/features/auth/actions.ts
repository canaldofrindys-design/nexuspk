"use server";

import { redirect } from "next/navigation";
import { apiSend } from "@/services/api";
import { clearSessionCookie, clientContext, getSessionToken, setSessionCookie } from "./session";
import type { FormState } from "./types";

function text(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === "string" ? value : "";
}

function captcha(form: FormData): string | undefined {
  return text(form, "cf-turnstile-response") || undefined;
}

/** Só aceita caminhos internos para evitar redirecionamento aberto. */
function safeNext(value: string): string {
  return value.startsWith("/") && !value.startsWith("//") && !value.startsWith("/\\") ? value : "/painel";
}

export async function registerAction(_: FormState, form: FormData): Promise<FormState> {
  const login = text(form, "login").trim();
  const email = text(form, "email").trim();
  const password = text(form, "password");
  const values = { login, email };

  if (password !== text(form, "passwordConfirm")) {
    return { status: "error", message: "Confira os campos destacados.", fieldErrors: { passwordConfirm: "As senhas não conferem." }, values };
  }

  const result = await apiSend<{ confirmationEmailSent: boolean }>("POST", "/api/v1/auth/register", {
    body: { login, password, email, captchaToken: captcha(form) },
    ...(await clientContext()),
  });

  if (!result.ok) {
    const fieldErrors = result.error.errors ?? undefined;
    // Com erro em campo, a mensagem aparece junto do campo; no topo fica só o aviso geral.
    const message = fieldErrors ? "Confira os campos destacados." : result.error.message;
    return { status: "error", message, fieldErrors, values };
  }

  return {
    status: "success",
    message: result.data.confirmationEmailSent
      ? `Conta ${login} criada. Enviamos um link de confirmação para ${email}. Você já pode entrar no jogo.`
      : `Conta ${login} criada. Você já pode entrar no jogo e no painel.`,
  };
}

export async function loginAction(_: FormState, form: FormData): Promise<FormState> {
  const login = text(form, "login").trim();
  const result = await apiSend<{ token: string; expiresUtc: string }>("POST", "/api/v1/auth/login", {
    body: { login, password: text(form, "password"), captchaToken: captcha(form) },
    ...(await clientContext()),
  });

  if (!result.ok) {
    return { status: "error", message: result.error.message, values: { login } };
  }

  await setSessionCookie(result.data.token, result.data.expiresUtc);
  redirect(safeNext(text(form, "next")));
}

export async function logoutAction(): Promise<void> {
  const token = await getSessionToken();
  if (token) {
    await apiSend("POST", "/api/v1/auth/logout", { token });
  }
  await clearSessionCookie();
  redirect("/");
}

export async function recoverAction(_: FormState, form: FormData): Promise<FormState> {
  const login = text(form, "login").trim();
  const email = text(form, "email").trim();
  const result = await apiSend("POST", "/api/v1/auth/recover", {
    body: { login, email, captchaToken: captcha(form) },
    ...(await clientContext()),
  });

  if (!result.ok) {
    return { status: "error", message: result.error.message, values: { login, email } };
  }

  return {
    status: "success",
    message:
      "Se o login e o e-mail conferem com uma conta de e-mail confirmado, enviamos um link para criar uma nova senha. O link vale por 30 minutos.",
  };
}

export async function resetPasswordAction(_: FormState, form: FormData): Promise<FormState> {
  const newPassword = text(form, "password");
  if (newPassword !== text(form, "passwordConfirm")) {
    return { status: "error", message: "Confira os campos destacados.", fieldErrors: { passwordConfirm: "As senhas não conferem." } };
  }

  const result = await apiSend("POST", "/api/v1/auth/reset-password", {
    body: { token: text(form, "token"), newPassword },
    ...(await clientContext()),
  });

  if (!result.ok) {
    return { status: "error", message: result.error.message, fieldErrors: result.error.errors ?? undefined };
  }

  return { status: "success", message: "Senha alterada. Use a nova senha no jogo e no painel." };
}

export async function confirmEmailAction(_: FormState, form: FormData): Promise<FormState> {
  const result = await apiSend("POST", "/api/v1/auth/confirm-email", {
    body: { token: text(form, "token") },
    ...(await clientContext()),
  });
  return result.ok
    ? { status: "success", message: "E-mail confirmado. Agora você pode recuperar a conta por ele." }
    : { status: "error", message: result.error.message };
}
