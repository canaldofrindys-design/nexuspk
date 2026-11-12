import Image from "next/image";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "./classes";

/**
 * Vitrine das 10 classes; cada uma leva à ficha na página Classes.
 * No celular vira um carrossel deslizável (com encaixe) em vez de uma grade de 5 linhas.
 */
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
      <ul className="class-rail -mx-3 flex snap-x snap-mandatory gap-3 overflow-x-auto px-3 pb-1 md:mx-0 md:grid md:grid-cols-5 md:overflow-visible md:px-0 md:pb-0">
        {classes.map((c) => (
          <li key={c.code} data-reveal="zoom" className="w-[40%] shrink-0 snap-start min-[480px]:w-[30%] md:w-auto">
            <Link
              href={`/classes#${c.code}`}
              className="group relative flex flex-col items-center px-1 pt-1 pb-2 text-center transition-colors hover:bg-steel/20"
            >
              <span
                data-tilt
                className="relative w-full overflow-hidden border border-gold/30 shadow-[0_14px_30px_-14px_rgb(0_0_0/0.9)] group-hover:border-gold/70 group-hover:shadow-[0_0_24px_rgb(95_178_255/0.35)]"
              >
                <Image
                  src={c.card}
                  alt=""
                  width={197}
                  height={265}
                  sizes="(min-width: 768px) 200px, 40vw"
                  className="aspect-[197/265] h-auto w-full object-cover transition-transform duration-300 group-hover:scale-105"
                />
              </span>
              <span className="mt-2 font-display text-sm text-gold-bright">{c.name}</span>
              <span className="text-xs text-silver/65">{c.role}</span>
            </Link>
          </li>
        ))}
      </ul>
      <p aria-hidden className="mt-2 text-center text-[11px] tracking-[0.25em] text-gold/60 uppercase md:hidden">
        Deslize para ver as {classes.length} classes
      </p>
    </GameWindow>
  );
}
