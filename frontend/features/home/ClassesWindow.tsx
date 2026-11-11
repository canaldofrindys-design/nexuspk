import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "./classes";

export function ClassesWindow() {
  return (
    <GameWindow
      title="Classes"
      actions={
        <Link href="/ranking" className="text-xs text-silver/80 hover:text-gold">
          Ranking
        </Link>
      }
    >
      <ul className="grid grid-cols-1 text-sm">
        {classes.map((c) => (
          <li key={c.code} className="flex justify-between gap-2 border-b border-steel/50 py-1.5">
            <Link href={`/ranking?classe=${c.code}`} className="text-data-name hover:text-gold-bright">
              {c.name}
            </Link>
            <span className="text-right text-xs text-silver/70">{c.role}</span>
          </li>
        ))}
      </ul>
    </GameWindow>
  );
}
