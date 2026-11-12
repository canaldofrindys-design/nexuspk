import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ClassIcon } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "@/features/home/classes";

export const metadata: Metadata = { title: "Classes", description: "As classes do NexusPK e o ranking de cada uma." };

export default function ClassesPage() {
  return (
    <>
      <section className="relative isolate overflow-hidden border-b border-gold/40">
        <Image src="/art/castelo.webp" alt="" fill sizes="100vw" className="-z-20 object-cover object-[50%_30%] opacity-60" />
        <div aria-hidden className="absolute inset-0 -z-10 bg-gradient-to-r from-night via-night/80 to-night/30" />
        <div className="mx-auto grid max-w-7xl items-end gap-4 px-4 pt-10 md:grid-cols-[1fr_380px]">
          <div className="self-center pb-8">
            <h1 className="text-gilded font-display text-4xl sm:text-5xl">Classes</h1>
            <p className="mt-3 max-w-xl text-silver/90">
              {classes.length} classes jogáveis. Cada uma tem ranking próprio por nível.
            </p>
          </div>
          <Image
            src="/art/heroi-verde.webp"
            alt=""
            width={1294}
            height={1100}
            priority
            sizes="(min-width: 768px) 380px, 0px"
            className="hidden h-auto w-full drop-shadow-[0_20px_40px_rgb(0_0_0/0.8)] md:block"
          />
        </div>
      </section>

      <div className="mx-auto w-full max-w-7xl px-4 py-10">
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          {classes.map((c, i) => (
            <li key={c.code} id={String(c.code)} className="min-w-0 scroll-mt-24">
              <GameWindow title={c.name} className="h-full">
                <div className="flex flex-col gap-2">
                  <div className="relative mx-auto w-full max-w-[240px] overflow-hidden border border-gold/30 shadow-[inset_0_0_24px_rgb(0_0_0/0.6)]">
                    <Image
                      src={c.card}
                      alt={`Ilustração da classe ${c.name}`}
                      width={197}
                      height={265}
                      sizes="240px"
                      priority={i < 5}
                      className="aspect-[197/265] h-auto w-full object-cover"
                    />
                    <span aria-hidden className="absolute inset-x-0 bottom-0 h-16 bg-gradient-to-t from-night/80 to-transparent" />
                  </div>
                  <div className="gold-rule my-1" />
                  <p className="flex items-center gap-2 text-sm text-silver/85">
                    <ClassIcon code={c.code} name={c.name} size={28} />
                    {c.role}
                  </p>
                  <Link href={`/ranking?classe=${c.code}`} className="text-sm text-arcane hover:text-gold">
                    Ranking de {c.name}
                  </Link>
                </div>
              </GameWindow>
            </li>
          ))}
        </ul>
        <p className="mt-6 text-xs text-silver/60">
          Ilustrações das classes: arte oficial do Priston Tale, direitos de seus respectivos detentores. Usadas aqui só para identificar as classes do jogo.
        </p>
      </div>
    </>
  );
}
