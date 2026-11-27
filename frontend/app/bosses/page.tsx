import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { PageHero } from "@/components/scene/PageHero";
import { BossTimers } from "@/features/bosses/BossTimers";
import { defaultSchedule } from "@/features/home/eventSchedule";
import { loadSite } from "@/features/site/loadSite";

export const metadata: Metadata = {
  title: "Horários de boss",
  description: "Horários dos bosses do NexusPK com contagem regressiva e aviso no navegador antes de cada boss.",
};

async function Body() {
  await connection();
  const site = await loadSite();
  const schedule = site?.schedule ?? defaultSchedule;
  const bosses = schedule.filter((e) => e.kind === "boss");
  const others = schedule.filter((e) => e.kind !== "boss");
  return (
    <>
      {!site ? <p className="mb-4 text-sm text-data-bad">Não foi possível carregar os horários agora. Tente de novo em instantes.</p> : null}
      <BossTimers bosses={bosses} others={others} />
    </>
  );
}

export default function BossesPage() {
  return (
    <>
      <PageHero
        scene="torre-vigia"
        icon="boss"
        eyebrow="O Jogo"
        title="Horários de boss"
        lead="Quando cada boss nasce, quanto falta e um aviso no navegador para você não perder o próximo. Horários de Brasília."
      />
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-10">
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando horários…</p>}>
          <Body />
        </Suspense>
        <p className="text-sm text-silver/70">
          Eventos com data marcada pela equipe, como bosses especiais e invasões, ficam na{" "}
          <Link href="/eventos?categoria=boss" className="text-arcane hover:underline">
            agenda de eventos
          </Link>
          . O que cada boss deixa cai no próprio mapa: veja os{" "}
          <Link href="/guias/sistemas#mapas" className="text-arcane hover:underline">
            sistemas do servidor
          </Link>
          .
        </p>
      </div>
    </>
  );
}
