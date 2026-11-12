import Link from "next/link";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { GameWindow } from "@/components/ui/GameWindow";
import { LoginForm } from "@/features/auth/forms";
import { getCurrentPlayer } from "@/features/auth/session";

/** Login direto da home; com sessão aberta, leva ao painel. */
export async function AccountWindow() {
  const player = await getCurrentPlayer();
  if (player) {
    return (
      <GameWindow title="Conta">
        <p className="mb-3 text-sm">
          Conectado como <span className="font-medium text-data-name">{player.login}</span>.
        </p>
        <ButtonLink href="/painel">Abrir painel</ButtonLink>
      </GameWindow>
    );
  }
  return (
    <GameWindow title="Conta">
      <LoginForm next="/painel" />
      <div className="mt-3 flex justify-between gap-2 text-sm">
        <Link href="/criar-conta" className="text-gold hover:underline">
          Criar conta
        </Link>
        <Link href="/recuperar-conta" className="text-silver/70 hover:text-gold hover:underline">
          Esqueci minha senha
        </Link>
      </div>
    </GameWindow>
  );
}
