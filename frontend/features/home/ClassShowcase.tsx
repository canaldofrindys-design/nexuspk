import Image from "next/image";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "./classes";

/** Vitrine das 10 classes; cada uma leva à ficha na página Classes. */
export function ClassShowcase() {
  return (
    <GameWindow
      title="Classes"
      actions={
        <Link href="/classes" className="text-xs text-silver/80 hover:text-gold">
          Ver as {classes.length} classes
        </Link>
      }
    >
      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-5">
        {classes.map((c) => (
          <li key={c.code}>
            <Link
              href={`/classes#${c.code}`}
              className="group relative flex flex-col items-center px-1 pt-1 pb-2 text-center transition-colors hover:bg-steel/20"
            >
              <span className="relative w-full overflow-hidden border border-gold/30 transition-colors group-hover:border-gold/70">
                <Image
                  src={c.card}
                  alt=""
                  width={197}
                  height={265}
                  sizes="(min-width: 768px) 200px, 45vw"
                  className="aspect-[197/265] h-auto w-full object-cover transition-transform duration-300 group-hover:scale-105"
                />
              </span>
              <span className="mt-2 font-display text-sm text-gold-bright">{c.name}</span>
              <span className="hidden text-xs text-silver/65 sm:block">{c.role}</span>
            </Link>
          </li>
        ))}
      </ul>
    </GameWindow>
  );
}
