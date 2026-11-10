import type { Metadata } from "next";
import { Suspense } from "react";
import { AuthPanel } from "@/components/forms/AuthPanel";
import { ResetPasswordForm } from "@/features/auth/forms";
import { loadAuthOptions } from "@/features/auth/loadOptions";
import { Notice } from "@/features/auth/Notice";

export const metadata: Metadata = { title: "Nova senha", robots: { index: false } };

async function ResetSection({ searchParams }: { searchParams: Promise<{ token?: string | string[] }> }) {
  const { token } = await searchParams;
  if (typeof token !== "string" || token.length !== 43) return <Notice tone="bad">Link inválido ou expirado. Peça um novo em Recuperar conta.</Notice>;
  const options = await loadAuthOptions();
  if (!options) return <Notice tone="bad">Não foi possível falar com o servidor do portal. Tente de novo em instantes.</Notice>;
  return <ResetPasswordForm token={token} options={options} />;
}

export default function ResetPasswordPage({ searchParams }: PageProps<"/recuperar-conta/nova-senha">) {
  return (
    <AuthPanel title="Criar nova senha">
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <ResetSection searchParams={searchParams as Promise<{ token?: string | string[] }>} />
      </Suspense>
    </AuthPanel>
  );
}
