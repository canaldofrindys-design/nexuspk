import type { Metadata } from "next";
import Link from "next/link";
import { GameWindow } from "@/components/ui/GameWindow";
import { classes } from "@/features/home/classes";

export const metadata: Metadata = { title: "Classes", description: "As classes do NexusPK e o ranking de cada uma." };

export default function ClassesPage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-4 font-display text-2xl text-gold-bright">Classes</h1>
      <ul className="grid items-start gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {classes.map((c, i) => (
          <li key={c.code} id={String(c.code)} className="min-w-0 scroll-mt-24">
            <GameWindow title={c.name}>
              <div className="flex flex-col gap-3">
                {"art" in c ? (
                  <div className="flex h-72 items-end justify-center">
                    {/* eslint-disable-next-line @next/next/no-img-element -- webp já otimizado em public/classes; caixa de altura fixa */}
                    <img
                      src={c.art}
                      alt={`Arte oficial da classe ${c.name}`}
                      width={300}
                      height={288}
                      loading={i < 3 ? "eager" : "lazy"}
                      decoding="async"
                      className="h-72 w-auto max-w-full object-contain drop-shadow-[0_8px_16px_rgb(0_0_0/0.6)]"
                    />
                  </div>
                ) : null}
                <p className="text-sm text-silver/80">{c.role}</p>
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
  );
}
