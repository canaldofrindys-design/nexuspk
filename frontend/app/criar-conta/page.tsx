import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { AuthPanel } from "@/components/forms/AuthPanel";
import { GameWindow } from "@/components/ui/GameWindow";
import { RegisterForm } from "@/features/auth/forms";
import { loadAuthOptions } from "@/features/auth/loadOptions";
import { Notice } from "@/features/auth/Notice";

export const metadata: Metadata = { title: "Criar conta" };

async function RegisterSection() {
  await connection();
  const options = await loadAuthOptions();
  if (!options) return <Notice tone="bad">Não foi possível falar com o servidor do portal. Tente de novo em instantes.</Notice>;
  if (!options.registrationOpen) return <Notice>O cadastro pelo site está fechado no momento. Acompanhe os avisos na página inicial.</Notice>;
  return <RegisterForm options={options} />;
}

export default function CreateAccountPage() {
  return (
    <AuthPanel
      title="Criar conta"
      aside={
        <GameWindow title="Antes de criar" as="aside">
          <ul className="flex flex-col gap-2 text-sm text-silver/80">
            <li>O login e a senha valem para o jogo e para o painel.</li>
            <li>Uma conta por e-mail.</li>
            <li>
              Já tem conta?{" "}
              <Link href="/login" className="text-arcane hover:underline">
                Entrar
              </Link>
            </li>
          </ul>
        </GameWindow>
      }
    >
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <RegisterSection />
      </Suspense>
    </AuthPanel>
  );
}
