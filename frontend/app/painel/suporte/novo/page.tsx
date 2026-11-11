import type { Metadata } from "next";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { NewTicketForm } from "@/features/support/forms";

export const metadata: Metadata = { title: "Abrir chamado", robots: { index: false } };

export default function NewTicketPage() {
  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-10">
      <p className="mb-2 text-sm">
        <Link href="/painel/suporte" className="text-arcane hover:underline">
          Meus chamados
        </Link>
      </p>
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Abrir chamado</h1>
      <GameWindow title="Novo chamado">
        <NewTicketForm />
      </GameWindow>
    </div>
  );
}
