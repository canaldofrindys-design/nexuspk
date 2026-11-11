/**
 * IP do jogador a partir do X-Forwarded-For. Usa a última entrada, que é a que o IIS acrescentou
 * (as anteriores podem vir forjadas pelo navegador). O módulo do IIS escreve "ip:porta"
 * ("127.0.0.1:52463" ou "[::1]:52463"); a porta sai, porque a API agrupa limites e auditoria por IP.
 */
export function lastForwardedIp(header: string | null | undefined): string | undefined {
  const last = header?.split(",").map((s) => s.trim()).filter(Boolean).at(-1);
  if (!last) return undefined;
  const bracketed = /^\[([0-9a-fA-F:.]+)\](?::\d+)?$/.exec(last);
  if (bracketed) return bracketed[1];
  const v4WithPort = /^(\d{1,3}(?:\.\d{1,3}){3}):\d+$/.exec(last);
  if (v4WithPort) return v4WithPort[1];
  return last;
}
