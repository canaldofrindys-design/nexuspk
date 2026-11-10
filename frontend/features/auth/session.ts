import "server-only";
import { cookies, headers } from "next/headers";
import { cache } from "react";
import { apiSend } from "@/services/api";

/** Cookie com o token de sessão. httpOnly: o JavaScript da página nunca lê o token. */
export const SESSION_COOKIE = "nx_session";

export type CurrentPlayer = {
  login: string;
  email: string | null;
  emailConfirmed: boolean;
  sessionExpiresUtc: string;
};

export async function getSessionToken(): Promise<string | undefined> {
  return (await cookies()).get(SESSION_COOKIE)?.value;
}

/** Jogador da sessão atual, ou null. Uma consulta por requisição. */
export const getCurrentPlayer = cache(async (): Promise<CurrentPlayer | null> => {
  const token = await getSessionToken();
  if (!token) return null;
  const result = await apiSend<CurrentPlayer>("GET", "/api/v1/auth/me", { token, ...(await clientContext()) });
  return result.ok ? result.data : null;
});

export async function setSessionCookie(token: string, expiresUtc: string) {
  (await cookies()).set(SESSION_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(expiresUtc),
  });
}

export async function clearSessionCookie() {
  (await cookies()).delete(SESSION_COOKIE);
}

/**
 * IP do jogador. O IIS repassa o IP real em X-Forwarded-For; usamos a última entrada,
 * que é a que o próprio IIS acrescentou (as anteriores podem vir forjadas pelo cliente).
 */
export async function clientContext() {
  const h = await headers();
  const forwarded = h.get("x-forwarded-for");
  const clientIp = forwarded?.split(",").map((s) => s.trim()).filter(Boolean).at(-1) ?? undefined;
  return { clientIp, userAgent: h.get("user-agent") ?? undefined };
}
