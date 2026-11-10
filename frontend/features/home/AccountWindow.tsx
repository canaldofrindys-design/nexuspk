import { LogIn, UserPlus } from "lucide-react";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { GameWindow } from "@/components/ui/GameWindow";

export function AccountWindow() {
  return (
    <GameWindow title="Conta">
      <p className="mb-3 text-sm text-silver/80">Use a mesma conta do jogo para entrar no painel.</p>
      <div className="flex flex-wrap gap-2">
        <ButtonLink href="/login" variant="arcane" icon={<LogIn className="size-4" aria-hidden />}>
          Entrar
        </ButtonLink>
        <ButtonLink href="/criar-conta" variant="ghost" icon={<UserPlus className="size-4" aria-hidden />}>
          Criar conta
        </ButtonLink>
      </div>
      <a href="/recuperar-conta" className="mt-3 inline-block text-xs text-silver/70 underline-offset-2 hover:text-gold hover:underline">
        Esqueci minha senha
      </a>
    </GameWindow>
  );
}
