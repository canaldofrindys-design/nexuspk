import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import type { RankedCharacter } from "./types";

export function RankingWindow({ players, available }: { players: RankedCharacter[]; available: boolean }) {
  return (
    <GameWindow
      title="Ranking"
      actions={
        <Link href="/ranking" className="text-xs text-silver/80 hover:text-gold">
          Ver ranking completo
        </Link>
      }
    >
      {!available ? (
        <p className="text-sm text-silver/70">Ranking indisponível no momento.</p>
      ) : players.length === 0 ? (
        <p className="text-sm text-silver/70">Nenhum personagem no ranking ainda.</p>
      ) : (
        <table className="w-full text-sm">
          <caption className="sr-only">Cinco personagens de maior nível</caption>
          <thead>
            <tr className="text-left text-xs text-silver/60">
              <th scope="col" className="w-8 py-1 font-normal">#</th>
              <th scope="col" className="py-1 font-normal">Personagem</th>
              <th scope="col" className="py-1 font-normal">Classe</th>
              <th scope="col" className="py-1 text-right font-normal">Nível</th>
            </tr>
          </thead>
          <tbody>
            {players.map((p) => (
              <tr key={p.name} className="border-t border-steel/60">
                <td className={`tabular py-1.5 ${p.position <= 3 ? "text-gold-bright" : "text-silver/70"}`}>{p.position}</td>
                <td className="py-1.5 font-medium text-data-name">{p.name}</td>
                <td className="py-1.5 text-silver/80">{p.className}</td>
                <td className="tabular py-1.5 text-right text-data-info">{p.level}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <p className="mt-2 text-xs text-silver/50">Classificação por nível. EXP entra quando o servidor passar a publicá-la.</p>
    </GameWindow>
  );
}
