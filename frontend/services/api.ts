import "server-only";

/**
 * Cliente da API do portal, usado apenas em Server Components.
 * PORTAL_API_INTERNAL_URL aponta para a API em localhost no servidor
 * (ex.: http://127.0.0.1:5080). Nenhuma credencial passa pelo navegador.
 */
const baseUrl = process.env.PORTAL_API_INTERNAL_URL ?? "http://127.0.0.1:5080";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(new URL(path, baseUrl), {
    ...init,
    headers: { Accept: "application/json", ...init?.headers },
    signal: init?.signal ?? AbortSignal.timeout(5000),
  });
  if (!response.ok) {
    throw new ApiError(response.status, `API respondeu ${response.status} em ${path}`);
  }
  return (await response.json()) as T;
}

export type ApiResult<T> =
  | { ok: true; status: number; data: T }
  | { ok: false; status: number; error: ApiErrorBody };

export type ApiErrorBody = {
  code: string;
  message: string;
  errors?: Record<string, string> | null;
};

const unreachable: ApiErrorBody = {
  code: "unreachable",
  message: "Não foi possível falar com o servidor do portal. Tente de novo em instantes.",
};

/**
 * Chamada que pode falhar de forma esperada (validação, login incorreto).
 * Nunca lança: devolve o corpo de erro da API ou uma mensagem genérica.
 */
export async function apiSend<T>(
  method: "GET" | "POST",
  path: string,
  options: { body?: unknown; token?: string; clientIp?: string; userAgent?: string } = {},
): Promise<ApiResult<T>> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (options.token) headers.Authorization = `Bearer ${options.token}`;
  if (options.clientIp) headers["X-Forwarded-For"] = options.clientIp;
  if (options.userAgent) headers["User-Agent"] = options.userAgent.slice(0, 256);

  let response: Response;
  try {
    response = await fetch(new URL(path, baseUrl), {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      cache: "no-store",
      signal: AbortSignal.timeout(10000),
    });
  } catch {
    return { ok: false, status: 0, error: unreachable };
  }

  const text = await response.text();
  let json: unknown = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }

  if (response.ok) return { ok: true, status: response.status, data: json as T };
  const body = json as Partial<ApiErrorBody> | null;
  if (body && typeof body.code === "string" && typeof body.message === "string") {
    return { ok: false, status: response.status, error: body as ApiErrorBody };
  }
  if (response.status === 429) {
    return { ok: false, status: 429, error: { code: "rate-limited", message: "Muitas tentativas. Aguarde um minuto." } };
  }
  return { ok: false, status: response.status, error: unreachable };
}
