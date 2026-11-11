import "server-only";
import { apiFetchRaw } from "@/services/api";

const allowed = new Set(["image/png", "image/jpeg", "image/bmp"]);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Repassa o anexo da API com cabeçalhos que impedem o navegador de tratá-lo como página. */
export async function proxyAttachment(apiPath: string, ticketId: string, attachmentId: string, token: string | undefined): Promise<Response> {
  const id = Number(ticketId);
  if (!token || !Number.isInteger(id) || id <= 0 || !uuid.test(attachmentId)) return new Response("Não encontrado", { status: 404 });
  const r = await apiFetchRaw(`${apiPath}/${id}/attachments/${attachmentId}`, token);
  const type = r?.headers.get("content-type")?.split(";")[0] ?? "";
  if (!r || !r.ok || !allowed.has(type)) return new Response("Não encontrado", { status: r?.status === 401 ? 401 : 404 });
  return new Response(r.body, {
    headers: {
      "Content-Type": type,
      "Content-Disposition": r.headers.get("content-disposition") ?? "inline",
      "Content-Security-Policy": "default-src 'none'; sandbox",
      "X-Content-Type-Options": "nosniff",
      "Cache-Control": "private, max-age=300",
    },
  });
}
