import { apiFetchPublic } from "@/services/api";

const allowed = new Set(["image/bmp", "image/png", "image/jpeg"]);

/** Imagem de um item do jogo (copiada do cliente para o VPS), pelo código do arquivo: /item-imagem/WA161. */
export async function GET(_: Request, ctx: RouteContext<"/item-imagem/[id]">) {
  const { id } = await ctx.params;
  if (!/^[A-Za-z]{2}[0-9]{3,4}$/.test(id)) return new Response("Não encontrado", { status: 404 });
  const r = await apiFetchPublic(`/api/v1/items/${id.toUpperCase()}/image`);
  const type = r?.headers.get("content-type")?.split(";")[0] ?? "";
  if (!r || !r.ok || !allowed.has(type)) return new Response("Não encontrado", { status: 404 });
  return new Response(r.body, {
    headers: {
      "Content-Type": type,
      "Cache-Control": "public, max-age=86400",
      "Content-Security-Policy": "default-src 'none'; sandbox",
      "X-Content-Type-Options": "nosniff",
    },
  });
}
