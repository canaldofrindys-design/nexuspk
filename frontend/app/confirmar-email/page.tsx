import type { Metadata } from "next";
import { Suspense } from "react";
import { AuthPanel } from "@/components/forms/AuthPanel";
import { ConfirmEmailForm } from "@/features/auth/forms";
import { Notice } from "@/features/auth/Notice";

export const metadata: Metadata = { title: "Confirmar e-mail", robots: { index: false } };

async function ConfirmSection({ searchParams }: { searchParams: Promise<{ token?: string | string[] }> }) {
  const { token } = await searchParams;
  if (typeof token !== "string" || token.length !== 43) return <Notice tone="bad">Link inválido ou expirado.</Notice>;
  return <ConfirmEmailForm token={token} />;
}

export default function ConfirmEmailPage({ searchParams }: PageProps<"/confirmar-email">) {
  return (
    <AuthPanel title="Confirmar e-mail">
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <ConfirmSection searchParams={searchParams as Promise<{ token?: string | string[] }>} />
      </Suspense>
    </AuthPanel>
  );
}
