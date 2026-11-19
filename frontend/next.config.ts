import type { NextConfig } from "next";

const isDev = process.env.NODE_ENV !== "production";

/**
 * Política de conteúdo: só scripts do próprio site e do Turnstile (captcha).
 * 'unsafe-inline' em script-src é exigido pelos scripts de inicialização do Next com
 * renderização parcial (nonce obrigaria toda página a ser dinâmica). Nenhum HTML vindo
 * de usuário é renderizado como HTML: textos de notícias, eventos e chamados saem como texto.
 */
const csp = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline'${isDev ? " 'unsafe-eval'" : ""} https://challenges.cloudflare.com`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob: https:",
  "font-src 'self'",
  "connect-src 'self' https://challenges.cloudflare.com",
  "frame-src https://challenges.cloudflare.com",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join("; ");

const nextConfig: NextConfig = {
  // Gera .next/standalone para rodar como serviço Node.js atrás do IIS.
  output: "standalone",
  poweredByHeader: false,
  cacheComponents: true,
  partialPrefetching: true,
  experimental: {
    // Chamado de suporte com até 3 imagens de 3 MB.
    serverActions: { bodySizeLimit: "12mb" },
    proxyClientMaxBodySize: "12mb",
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  /**
   * Endereços do site antigo (PHP) que jogadores, buscadores e favoritos ainda usam.
   * As pastas que o jogo e o launcher chamam (ServerMain, Clan, ClanContent, updater...)
   * são aplicações do IIS e nem chegam aqui. Ver docs/DEPLOYMENT.md, "Troca do site".
   */
  async redirects() {
    const legacy: [string, string][] = [
      ["/index.php", "/"],
      ["/painel/cadastrar", "/criar-conta"],
      ["/painel/ativar", "/confirmar-email"],
      ["/painel/recuperar", "/recuperar-conta"],
      ["/painel/login", "/login"],
      ["/painel/logado", "/painel"],
      ["/painel/sair", "/login"],
      ["/painel/support/:path*", "/painel/suporte"],
      ["/painel/admin/:path*", "/admin"],
      ["/painel/:old(dashboard|settings|nick|classe|skills|status|notification|personagens|donate|mydonate|confirmardonate|banido)/:path*", "/painel"],
      ["/rankings/:path*", "/ranking"],
      ["/rank/:path*", "/ranking"],
      ["/wiki/regras", "/guias/regras"],
      ["/wiki/aging", "/guias/aging"],
      ["/wiki/:path*", "/guias"],
      ["/notice/:path*", "/noticias"],
      ["/logs/:path*", "/ranking"],
    ];
    return [
      // www.<domínio> -> <domínio>, mantendo o caminho.
      {
        source: "/:path*",
        has: [{ type: "host" as const, value: "www\\.(?<domain>.+)" }],
        destination: "https://:domain/:path*",
        permanent: true,
      },
      ...legacy.map(([source, destination]) => ({ source, destination, permanent: true })),
    ];
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: csp },
          { key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" },
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
      // Anexos de chamado e imagens do painel: só imagem, nunca interpretada como página (a última regra vence).
      ...["/painel/suporte/:id/anexo/:anexo", "/admin/chamados/:id/anexo/:anexo", "/midia/:id"].map((source) => ({
        source,
        headers: [{ key: "Content-Security-Policy", value: "default-src 'none'; sandbox" }],
      })),
    ];
  },
};

export default nextConfig;
