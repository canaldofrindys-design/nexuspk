import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";

export default function NotFound() {
  return (
    <div className="mx-auto w-full max-w-xl px-4 py-16">
      <GameWindow title="Página não encontrada">
        <p className="text-sm text-silver/80">O endereço não existe ou o conteúdo ainda não foi publicado.</p>
        <p className="mt-3 flex flex-wrap gap-4 text-sm">
          <Link href="/" className="text-arcane hover:underline">
            Início
          </Link>
          <Link href="/noticias" className="text-arcane hover:underline">
            Notícias
          </Link>
          <Link href="/eventos" className="text-arcane hover:underline">
            Eventos
          </Link>
        </p>
      </GameWindow>
    </div>
  );
}
