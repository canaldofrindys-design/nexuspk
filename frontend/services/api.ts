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
