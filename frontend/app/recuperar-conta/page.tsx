import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { AuthPanel } from "@/components/forms/AuthPanel";
import { GameWindow } from "@/components/ui/GameWindow";
import { RecoverForm } from "@/features/auth/forms";
import { loadAuthOptions } from "@/features/auth/loadOptions";
import { Notice } from "@/features/auth/Notice";

export const metadata: Metadata = { title: "Recuperar conta" };

async function RecoverSection() {
  await connection();
  const options = await loadAuthOptions();
  if (!options) return <Notice tone="bad">Não foi possível falar com o servidor do portal. Tente de novo em instantes.</Notice>;
  if (!options.recoveryAvailable) {
    return (
      <Notice>
        A recuperação por e-mail ainda não está ativa. Abra um chamado em{" "}
        <Link href="/suporte" className="underline">
          Suporte
        </Link>
        .
      </Notice>
    );
  }
  return <RecoverForm />;
}

export default function RecoverPage() {
  return (
    <AuthPanel
      title="Recuperar conta"
      aside={
        <GameWindow title="Como funciona" as="aside">
          <ul className="flex flex-col gap-2 text-sm text-silver/80">
            <li>Informe o login e o e-mail confirmado da conta.</li>
            <li>O link chega por e-mail e vale por 30 minutos.</li>
            <li>Conta sem e-mail confirmado: entre no painel e cadastre um, ou fale com o suporte.</li>
          </ul>
        </GameWindow>
      }
    >
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <RecoverSection />
      </Suspense>
    </AuthPanel>
  );
}
