const baseUrl = process.env.PORTAL_API_INTERNAL_URL ?? "http://127.0.0.1:5080";
const slugPattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** Repassa o arquivo .ics gerado pela API (o navegador nunca fala direto com a API). */
export async function GET(_: Request, { params }: RouteContext<"/eventos/[slug]/ics">) {
  const { slug } = await params;
  if (!slugPattern.test(slug) || slug.length > 120) return new Response("Não encontrado", { status: 404 });
  let response: Response;
  try {
    response = await fetch(new URL(`/api/v1/events/${slug}/ics`, baseUrl), { cache: "no-store", signal: AbortSignal.timeout(5000) });
  } catch {
    return new Response("Serviço indisponível", { status: 503 });
  }
  if (!response.ok) return new Response("Não encontrado", { status: response.status === 404 ? 404 : 502 });
  return new Response(await response.arrayBuffer(), {
    headers: {
      "Content-Type": "text/calendar; charset=utf-8",
      "Content-Disposition": `attachment; filename="${slug}.ics"`,
      "Cache-Control": "public, max-age=60",
    },
  });
}
