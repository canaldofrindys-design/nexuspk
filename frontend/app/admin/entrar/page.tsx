import type { Metadata } from "next";
import { GameWindow } from "@/components/ui/GameWindow";
import { StaffLoginForm } from "@/features/admin/forms";

export const metadata: Metadata = { title: "Entrar" };

export default function StaffLoginPage() {
  return (
    <div className="mx-auto w-full max-w-sm px-4 py-16">
      <GameWindow title="Painel da equipe">
        <p className="mb-3 text-sm text-silver/70">Acesso restrito à equipe do servidor. Jogadores entram pela página Entrar do site.</p>
        <StaffLoginForm />
      </GameWindow>
    </div>
  );
}
