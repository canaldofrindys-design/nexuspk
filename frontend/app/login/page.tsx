import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { AuthPanel } from "@/components/forms/AuthPanel";
import { GameWindow } from "@/components/ui/GameWindow";
import { LoginForm } from "@/features/auth/forms";

export const metadata: Metadata = { title: "Entrar" };

async function LoginSection({ searchParams }: { searchParams: Promise<{ next?: string | string[] }> }) {
  const { next } = await searchParams;
  return <LoginForm next={typeof next === "string" ? next : "/painel"} />;
}

export default function LoginPage({ searchParams }: PageProps<"/login">) {
  return (
    <AuthPanel
      title="Entrar"
      aside={
        <GameWindow title="Conta" as="aside">
          <ul className="flex flex-col gap-2 text-sm text-silver/80">
            <li>Use o mesmo login e senha do jogo.</li>
            <li>
              <Link href="/criar-conta" className="text-arcane hover:underline">
                Criar conta
              </Link>
            </li>
            <li>
              <Link href="/recuperar-conta" className="text-arcane hover:underline">
                Esqueci minha senha
              </Link>
            </li>
          </ul>
        </GameWindow>
      }
    >
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <LoginSection searchParams={searchParams as Promise<{ next?: string | string[] }>} />
      </Suspense>
    </AuthPanel>
  );
}
