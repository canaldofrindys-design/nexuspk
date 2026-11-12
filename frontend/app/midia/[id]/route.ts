import { apiFetchPublic } from "@/services/api";

const allowed = new Set(["image/png", "image/jpeg"]);

/** Imagem enviada pela equipe no painel (notícias, eventos, banners). O id é aleatório e o arquivo nunca muda. */
export async function GET(_: Request, ctx: RouteContext<"/midia/[id]">) {
  const { id } = await ctx.params;
  if (!/^[0-9a-f]{32}$/i.test(id)) return new Response("Não encontrado", { status: 404 });
  const r = await apiFetchPublic(`/api/v1/media/${id}`);
  const type = r?.headers.get("content-type")?.split(";")[0] ?? "";
  if (!r || !r.ok || !allowed.has(type)) return new Response("Não encontrado", { status: 404 });
  return new Response(r.body, {
    headers: {
      "Content-Type": type,
      "Cache-Control": "public, max-age=31536000, immutable",
      "Content-Security-Policy": "default-src 'none'; sandbox",
      "X-Content-Type-Options": "nosniff",
    },
  });
}
