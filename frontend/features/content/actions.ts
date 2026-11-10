"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { apiSend } from "@/services/api";
import { clientContext, getSessionToken } from "@/features/auth/session";
import type { FormState } from "@/features/auth/types";

const slugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export async function reminderAction(_: FormState, form: FormData): Promise<FormState> {
  const slug = String(form.get("slug") ?? "");
  const enable = form.get("enable") === "1";
  if (!slugPattern.test(slug) || slug.length > 120) return { status: "error", message: "Evento inválido." };
  const token = await getSessionToken();
  if (!token) redirect(`/login?next=/eventos/${slug}`);

  const result = await apiSend(enable ? "PUT" : "DELETE", `/api/v1/player/events/${slug}/reminder`, {
    token,
    ...(await clientContext()),
  });
  if (!result.ok) {
    if (result.status === 401) redirect(`/login?next=/eventos/${slug}`);
    return { status: "error", message: result.error.message };
  }
  revalidatePath(`/eventos/${slug}`);
  return {
    status: "success",
    message: enable ? "Lembrete ativado. Você recebe um e-mail cerca de uma hora antes do início." : "Lembrete desativado.",
  };
}
