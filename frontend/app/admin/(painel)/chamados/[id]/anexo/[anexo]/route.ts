import { getStaffToken } from "@/features/admin/session";
import { proxyAttachment } from "@/features/support/proxyFile";

export async function GET(_: Request, ctx: RouteContext<"/admin/chamados/[id]/anexo/[anexo]">) {
  const { id, anexo } = await ctx.params;
  return proxyAttachment("/api/v1/admin/tickets", id, anexo, await getStaffToken());
}
