import { getSessionToken } from "@/features/auth/session";
import { proxyAttachment } from "@/features/support/proxyFile";

export async function GET(_: Request, ctx: RouteContext<"/painel/suporte/[id]/anexo/[anexo]">) {
  const { id, anexo } = await ctx.params;
  return proxyAttachment("/api/v1/player/tickets", id, anexo, await getSessionToken());
}
