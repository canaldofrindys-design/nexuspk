import Image from "next/image";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "./classes";

const withArt = classes.filter((c): c is Extract<(typeof classes)[number], { art: string }> => "art" in c);

/** Vitrine das classes com ilustração; cada uma leva à ficha na página Classes. */
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
      <ul className="grid grid-cols-3 gap-2 md:grid-cols-6">
        {withArt.map((c) => (
          <li key={c.code}>
            <Link
              href={`/classes#${c.code}`}
              className="group relative flex flex-col items-center px-1 pt-2 pb-3 text-center transition-colors hover:bg-steel/20"
            >
              <span
                aria-hidden
                className="absolute inset-x-3 bottom-12 h-10 rounded-[50%] bg-[radial-gradient(closest-side,rgb(95_178_255/0.35),transparent)] transition-opacity group-hover:opacity-100 md:opacity-70"
              />
              <Image
                src={c.art}
                alt=""
                width={300}
                height={480}
                sizes="(min-width: 768px) 180px, 30vw"
                className="relative h-32 w-auto object-contain drop-shadow-[0_10px_14px_rgb(0_0_0/0.7)] transition-transform duration-300 group-hover:-translate-y-1 sm:h-44 md:h-52"
              />
              <span className="mt-2 font-display text-sm text-gold-bright">{c.name}</span>
              <span className="hidden text-xs text-silver/65 sm:block">{c.role}</span>
            </Link>
          </li>
        ))}
      </ul>
    </GameWindow>
  );
}
