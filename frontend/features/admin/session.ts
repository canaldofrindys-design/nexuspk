import "server-only";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { cache } from "react";
import { apiSend } from "@/services/api";
import { clientContext } from "@/features/auth/session";
import type { Capability, StaffMe } from "./types";

/** Cookie da equipe: separado do cookie de jogador, só vale em /admin. */
export const STAFF_COOKIE = "nx_staff";

export async function getStaffToken(): Promise<string | undefined> {
  return (await cookies()).get(STAFF_COOKIE)?.value;
}

export const getStaff = cache(async (): Promise<StaffMe | null> => {
  const token = await getStaffToken();
  if (!token) return null;
  const r = await apiSend<StaffMe>("GET", "/api/v1/admin/me", { token, ...(await clientContext()) });
  return r.ok ? r.data : null;
});

/** Membro logado com a permissão pedida. Sem sessão vai ao login; com senha temporária vai à troca de senha. */
export async function requireStaff(capability?: Capability): Promise<{ me: StaffMe; token: string }> {
  const token = await getStaffToken();
  const me = await getStaff();
  if (!me || !token) redirect("/admin/entrar");
  if (me.mustChangePassword) redirect("/admin/senha");
  if (capability && !me.capabilities.includes(capability)) notFound();
  return { me, token };
}

export async function setStaffCookie(token: string, expiresUtc: string) {
  (await cookies()).set(STAFF_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict",
    path: "/admin",
    expires: new Date(expiresUtc),
  });
}

export async function clearStaffCookie() {
  (await cookies()).delete({ name: STAFF_COOKIE, path: "/admin" });
}
