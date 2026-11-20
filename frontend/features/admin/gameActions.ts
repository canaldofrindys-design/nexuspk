"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { apiSend } from "@/services/api";
import { clientContext } from "@/features/auth/session";
import type { FormState } from "@/features/auth/types";
import { getStaffToken } from "./session";
import type { GameOk } from "./types";

function text(form: FormData, name: string): string {
  const v = form.get(name);
  return typeof v === "string" ? v : "";
}

function num(form: FormData, name: string): number | null {
  const raw = text(form, name).trim();
  if (raw === "") return null;
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

async function token(): Promise<string> {
  const t = await getStaffToken();
  if (!t) redirect("/admin/entrar");
  return t;
}

/** Envia para a API e devolve a mensagem para o formulário. A senha nunca volta nos valores. */
async function send(method: "POST" | "PUT", path: string, body: unknown, refresh: string[], values?: Record<string, string>, timeoutMs?: number): Promise<FormState> {
  const r = await apiSend<GameOk>(method, path, { token: await token(), body, timeoutMs, ...(await clientContext()) });
  if (!r.ok) {
    if (r.status === 401) redirect("/admin/entrar");
    if (r.error.code === "must-change-password") redirect("/admin/senha");
    const fieldErrors = r.error.errors ?? undefined;
    return { status: "error", message: r.error.message, fieldErrors, values };
  }
  for (const p of refresh) revalidatePath(p);
  return { status: "success", message: r.data.message ?? "Feito." };
}

const account = (form: FormData) => encodeURIComponent(text(form, "login"));
const accountPage = (form: FormData) => `/admin/contas/${account(form)}`;

export async function banAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { reason: text(form, "reason"), days: text(form, "days") };
  return send("POST", `/api/v1/admin/accounts/${account(form)}/ban`, { reason: values.reason, days: num(form, "days"), password: text(form, "password") }, [accountPage(form)], values);
}

export async function unbanAction(_: FormState, form: FormData): Promise<FormState> {
  return send("POST", `/api/v1/admin/accounts/${account(form)}/unban`, { reason: text(form, "reason") }, [accountPage(form)], { reason: text(form, "reason") });
}

export async function coinsAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { mode: text(form, "mode"), amount: text(form, "amount"), reason: text(form, "reason") };
  return send("POST", `/api/v1/admin/accounts/${account(form)}/coins`, { mode: values.mode, amount: num(form, "amount"), reason: values.reason }, [accountPage(form)], values);
}

export async function vipAddAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { plan: text(form, "plan"), character: text(form, "character"), days: text(form, "days"), reason: text(form, "reason") };
  return send(
    "POST",
    `/api/v1/admin/accounts/${account(form)}/vip`,
    { plan: num(form, "plan"), character: values.character || null, days: num(form, "days"), reason: values.reason },
    [accountPage(form)],
    values,
  );
}

function vipRow(form: FormData) {
  try {
    return JSON.parse(text(form, "row")) as unknown;
  } catch {
    return null;
  }
}

export async function vipExtendAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { days: text(form, "days"), reason: text(form, "reason") };
  return send("POST", `/api/v1/admin/accounts/${account(form)}/vip/extend`, { row: vipRow(form), days: num(form, "days"), reason: values.reason }, [accountPage(form)], values);
}

export async function vipRemoveAction(_: FormState, form: FormData): Promise<FormState> {
  return send(
    "POST",
    `/api/v1/admin/accounts/${account(form)}/vip/remove`,
    { row: vipRow(form), reason: text(form, "reason"), password: text(form, "password") },
    [accountPage(form)],
    { reason: text(form, "reason") },
  );
}

export async function serverAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { reason: text(form, "reason") };
  return send(
    "POST",
    "/api/v1/admin/server",
    { action: text(form, "action"), target: text(form, "target"), confirm: text(form, "confirm"), reason: values.reason, password: text(form, "password") },
    ["/admin/servidor"],
    values,
    10 * 60 * 1000,
  );
}

export async function rankingResetAction(_: FormState, form: FormData): Promise<FormState> {
  const values = { reason: text(form, "reason") };
  return send(
    "POST",
    "/api/v1/admin/rankings/reset",
    { kind: text(form, "kind"), confirm: text(form, "confirm"), reason: values.reason, password: text(form, "password") },
    ["/admin/rankings"],
    values,
    120000,
  );
}

export async function rankingRestoreAction(_: FormState, form: FormData): Promise<FormState> {
  const id = Number(text(form, "id"));
  if (!Number.isInteger(id) || id <= 0) return { status: "error", message: "Cópia inválida." };
  return send("POST", `/api/v1/admin/rankings/backups/${id}/restore`, { reason: text(form, "reason"), password: text(form, "password") }, ["/admin/rankings"], { reason: text(form, "reason") }, 120000);
}

/** Campos com prefixo "k:" (chave) e "v:" (valor) viram objetos. "n:<col>" marca chave nula. */
function rowParts(form: FormData) {
  const keys: Record<string, string | null> = {};
  const values: Record<string, string> = {};
  for (const [name, v] of form.entries()) {
    if (typeof v !== "string") continue;
    if (name.startsWith("k:")) keys[name.slice(2)] = v;
    else if (name.startsWith("n:")) keys[name.slice(2)] = null;
    else if (name.startsWith("v:")) values[name.slice(2)] = v;
  }
  return { keys, values };
}

const fn = (form: FormData) => encodeURIComponent(text(form, "fn"));

export async function rowUpdateAction(_: FormState, form: FormData): Promise<FormState> {
  const { keys, values } = rowParts(form);
  return send("PUT", `/api/v1/admin/game-tables/${fn(form)}/rows`, { keys, values, reason: text(form, "reason") }, [`/admin/jogo/${fn(form)}`], {
    ...Object.fromEntries(Object.entries(values).map(([k, v]) => [`v:${k}`, v])),
    reason: text(form, "reason"),
  });
}

export async function rowInsertAction(_: FormState, form: FormData): Promise<FormState> {
  const { values } = rowParts(form);
  return send("POST", `/api/v1/admin/game-tables/${fn(form)}/rows`, { values, reason: text(form, "reason") }, [`/admin/jogo/${fn(form)}`], {
    ...Object.fromEntries(Object.entries(values).map(([k, v]) => [`v:${k}`, v])),
    reason: text(form, "reason"),
  });
}

export async function rowDeleteAction(_: FormState, form: FormData): Promise<FormState> {
  const { keys } = rowParts(form);
  return send("POST", `/api/v1/admin/game-tables/${fn(form)}/rows/delete`, { keys, reason: text(form, "reason"), password: text(form, "password") }, [`/admin/jogo/${fn(form)}`], {
    reason: text(form, "reason"),
  });
}

export async function sourceConfigAction(_: FormState, form: FormData): Promise<FormState> {
  const list = (name: string) => form.getAll(name).filter((v): v is string => typeof v === "string" && v !== "");
  return send(
    "PUT",
    `/api/v1/admin/game-tables/${fn(form)}/config`,
    {
      database: text(form, "database"),
      table: text(form, "table"),
      keyColumns: list("keyColumns"),
      editableColumns: list("editableColumns"),
      searchColumns: list("searchColumns"),
      accountColumn: text(form, "accountColumn") || null,
      allowInsert: form.get("allowInsert") === "on",
      allowDelete: form.get("allowDelete") === "on",
      newestFirst: form.get("newestFirst") === "on",
      password: text(form, "password"),
    },
    ["/admin/jogo", `/admin/jogo/${fn(form)}`, `/admin/jogo/${fn(form)}/configurar`],
  );
}

export async function sourceRemoveAction(_: FormState, form: FormData): Promise<FormState> {
  return send("POST", `/api/v1/admin/game-tables/${fn(form)}/config/remove`, { password: text(form, "password") }, ["/admin/jogo", `/admin/jogo/${fn(form)}/configurar`]);
}
