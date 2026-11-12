import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { CommunityLinks } from "@/features/site/CommunityLinks";
import type { HomeSnapshot } from "./types";

/** Ficha do servidor no formato de tooltip de item. Só mostra taxas configuradas pelo admin. */
export function ServerSheet({ server }: { server: HomeSnapshot["server"] }) {
  const rates = Object.entries(server.rates);
  return (
    <GameWindow title={`Ficha do servidor ${server.name}`}>
      <div className="grid gap-4 text-sm sm:grid-cols-3">
        <div>
          <p className="font-display text-data-name">{server.name}</p>
          <p className="text-silver/80">{server.tagline || "Servidor privado de Priston Tale."}</p>
          <p className="mt-2 flex flex-wrap gap-x-3 text-sm">
            <Link href="/servidor" className="text-arcane hover:underline">
              Sobre o servidor
            </Link>
            <Link href="/download" className="text-arcane hover:underline">
              Baixar o jogo
            </Link>
          </p>
          <CommunityLinks links={server.communityLinks} className="mt-3" />
        </div>
        <dl className="grid grid-cols-[1fr_auto] gap-y-1">
          {rates.length === 0 ? (
            <>
              <dt className="text-silver/70">Taxas</dt>
              <dd className="text-right text-silver/60">a publicar</dd>
            </>
          ) : (
            rates.map(([k, v]) => (
              <div key={k} className="contents">
                <dt className="text-silver/80">{k}</dt>
                <dd className="tabular text-right text-data-info">{v}</dd>
              </div>
            ))
          )}
        </dl>
        <dl className="grid grid-cols-[1fr_auto] gap-y-1">
          <dt className="text-silver/80">Horário dos eventos</dt>
          <dd className="text-right text-data-info">Brasília</dd>
        </dl>
      </div>
    </GameWindow>
  );
}
