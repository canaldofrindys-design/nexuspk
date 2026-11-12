/** Link externo só se for https; link interno só se for caminho do próprio site. */
export function safeHref(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith("/") && !url.startsWith("//") && !url.includes("\\")) return url;
  try {
    const u = new URL(url);
    return u.protocol === "https:" && !u.username && !u.password ? url : null;
  } catch {
    return null;
  }
}

export const isExternal = (url: string) => url.startsWith("https://");
