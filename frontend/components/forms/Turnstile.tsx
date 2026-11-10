"use client";

import Script from "next/script";

/**
 * Cloudflare Turnstile. Só aparece quando NEXT_PUBLIC_TURNSTILE_SITE_KEY está definido;
 * o widget acrescenta o campo cf-turnstile-response ao formulário.
 */
export function Turnstile() {
  const siteKey = process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY;
  if (!siteKey) return null;
  return (
    <>
      <Script src="https://challenges.cloudflare.com/turnstile/v0/api.js" strategy="afterInteractive" async defer />
      <div className="cf-turnstile" data-sitekey={siteKey} data-theme="dark" data-language="pt-br" />
    </>
  );
}
