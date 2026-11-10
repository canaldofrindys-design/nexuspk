import { Suspense } from "react";
import { ApiStatus } from "@/features/system/ApiStatus";

export default function HomePage() {
  return (
    <section className="mx-auto flex max-w-7xl flex-col items-start gap-6 px-4 py-24">
      <p className="text-sm uppercase tracking-[0.3em] text-arcane">Priston Tale</p>
      <h1 className="font-display text-4xl text-gold sm:text-6xl">NexusPK</h1>
      <p className="max-w-xl text-silver/80">
        Portal em construção. A homepage completa, com notícias, eventos, ranking e status do servidor, chega na
        próxima fase.
      </p>
      <Suspense fallback={<p className="text-sm text-silver/60">Verificando a API…</p>}>
        <ApiStatus />
      </Suspense>
    </section>
  );
}
