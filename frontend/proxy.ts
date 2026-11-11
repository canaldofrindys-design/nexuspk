import { NextResponse, type NextRequest } from "next/server";

/**
 * Sem cookie de sessão, o painel do jogador manda para o login e o painel da equipe para /admin/entrar.
 * A validade da sessão é conferida em cada página.
 */
export function proxy(request: NextRequest) {
  const path = request.nextUrl.pathname;
  if (path.startsWith("/admin")) {
    if (path === "/admin/entrar" || request.cookies.has("nx_staff")) return NextResponse.next();
    const url = request.nextUrl.clone();
    url.pathname = "/admin/entrar";
    url.search = "";
    return NextResponse.redirect(url);
  }
  if (!request.cookies.has("nx_session")) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    url.search = `?next=${encodeURIComponent(request.nextUrl.pathname)}`;
    return NextResponse.redirect(url);
  }
  return NextResponse.next();
}

export const config = { matcher: ["/painel/:path*", "/admin/:path*"] };
