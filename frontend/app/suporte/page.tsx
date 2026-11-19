import { PageHero } from "@/components/scene/PageHero";
import type { Metadata } from "next";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { ticketCategories } from "@/features/support/types";
import { ArtLink, kitArt } from "@/components/ui/ArtLink";

export const metadata: Metadata = {
  title: "Suporte",
  description: "Abra um chamado para a equipe, acompanhe as respostas e confira o status do servidor.",
};

export default function SupportPage() {
  return (
    <>
      <PageHero
        scene="vila-conversa"
        icon="guia"
        eyebrow="Fale com a equipe"
        title="Suporte"
        lead="Abra um chamado pela sua conta, anexe imagens do problema e acompanhe cada resposta da equipe pelo painel."
      />
    <div className="mx-auto w-full max-w-5xl px-4 py-10">
      <div className="grid gap-4 md:grid-cols-2">
        <GameWindow title="Chamados" icon="guia">
          <p className="text-sm text-silver/80">Entre com a sua conta do jogo para abrir um chamado e acompanhar as respostas da equipe.</p>
          <p className="mt-3 flex flex-wrap items-center gap-4 text-sm">
            <ArtLink href="/painel/suporte/novo" art={kitArt.botoes.suporte} label="Abrir chamado no suporte" sizes="180px" className="w-[180px]" />
            <Link href="/painel/suporte" className="self-center text-arcane hover:underline">
              Ver meus chamados
            </Link>
          </p>
          <h2 className="mt-4 mb-1 text-sm font-semibold text-silver">Categorias</h2>
          <ul className="grid grid-cols-2 gap-x-4 text-sm text-silver/80">
            {Object.values(ticketCategories).map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </GameWindow>
        <GameWindow title="Antes de abrir">
          <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-silver/80">
            <li>
              Servidor fora do ar? Veja a{" "}
              <Link href="/status" className="text-arcane hover:underline">
                página de status
              </Link>{" "}
              e as manutenções programadas.
            </li>
            <li>
              Esqueceu a senha?{" "}
              <Link href="/recuperar-conta" className="text-arcane hover:underline">
                Recupere a conta
              </Link>{" "}
              pelo e-mail cadastrado.
            </li>
            <li>A equipe nunca pede a sua senha, nem no chamado nem no jogo.</li>
            <li>Cada conta pode ter até 3 chamados em andamento.</li>
          </ul>
        </GameWindow>
      </div>
    </div>
    </>
  );
}
