import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
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
                  <div className="relative flex h-60 items-end justify-center">
                    <span
                      aria-hidden
                      className="absolute inset-x-4 bottom-0 h-12 rounded-[50%] bg-[radial-gradient(closest-side,rgb(95_178_255/0.35),transparent)]"
                    />
                    {"art" in c ? (
                      <Image
                        src={c.art}
                        alt={`Arte oficial da classe ${c.name}`}
                        width={300}
                        height={480}
                        sizes="240px"
                        priority={i < 5}
                        className="relative h-60 w-auto max-w-full object-contain drop-shadow-[0_8px_16px_rgb(0_0_0/0.6)]"
                      />
                    ) : (
                      <div className="relative mb-6 flex flex-col items-center gap-2 text-center">
                        <span className="grid size-24 place-items-center rounded-full border border-gold/50 bg-night/60 font-display text-5xl text-gold/80 shadow-[inset_0_0_24px_rgb(95_178_255/0.25)]">
                          {c.name[0]}
                        </span>
                        <span className="text-xs text-silver/50">Ilustração em breve</span>
                      </div>
                    )}
                  </div>
                  <div className="gold-rule my-1" />
                  <p className="text-sm text-silver/85">{c.role}</p>
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
