import { NextResponse, type NextRequest } from "next/server";

/** Sem cookie de sessão, o painel manda para o login. A validade da sessão é conferida na página. */
export function proxy(request: NextRequest) {
  if (!request.cookies.has("nx_session")) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    url.search = `?next=${encodeURIComponent(request.nextUrl.pathname)}`;
    return NextResponse.redirect(url);
  }
  return NextResponse.next();
}

export const config = { matcher: ["/painel/:path*"] };
