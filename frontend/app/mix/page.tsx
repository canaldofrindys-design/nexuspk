import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { PageHero } from "@/components/scene/PageHero";
import { Prose } from "@/features/content/Prose";
import { formatDate } from "@/features/home/format";
import { MixTable } from "@/features/mix/MixTable";
import type { MixSettings, SettingView } from "@/features/site/types";
import { apiGet } from "@/services/api";

export const metadata: Metadata = {
  title: "Mix List",
  description: "Mix List do NexusPK: as fórmulas de mix de cada equipamento, com os sheltoms e o bônus de cada uma.",
};

async function Body() {
  await connection();
  let mix: SettingView<MixSettings> | null = null;
  try {
    mix = await apiGet<SettingView<MixSettings>>("/api/v1/site/mix");
  } catch {
    mix = null;
  }
  if (!mix) return <p className="text-sm text-data-bad">A Mix List está indisponível no momento. Tente de novo em instantes.</p>;
  const { intro, items } = mix.value;
  return (
    <>
      {intro ? (
        <div className="space-y-3 text-sm leading-relaxed text-silver/90">
          <Prose text={intro} />
        </div>
      ) : null}
      {items.length === 0 ? (
        <p className="game-window px-4 py-4 text-sm text-silver/80">A equipe está publicando a Mix List do servidor. Volte em breve.</p>
      ) : (
        <MixTable items={items} />
      )}
      {mix.updatedUtc ? <p className="text-xs text-silver/50">Atualizada em {formatDate(mix.updatedUtc)}.</p> : null}
    </>
  );
}

export default function MixPage() {
  return (
    <>
      <PageHero
        scene="mercado-entardecer"
        icon="recompensas"
        eyebrow="O Jogo · Evolução"
        title="Mix List"
        lead="Cada mix coloca sheltoms no equipamento para dar um bônus fixo. Aqui estão as fórmulas do NexusPK, por equipamento."
      />
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-4 py-10">
        <Suspense fallback={<p className="text-sm text-silver/60">Carregando a Mix List…</p>}>
          <Body />
        </Suspense>
        <p className="text-sm text-silver/70">
          Vai aprimorar com Aging?{" "}
          <Link href="/guias/aging" className="text-arcane hover:underline">
            Veja a tabela e a calculadora de sheltoms
          </Link>
          .
        </p>
      </div>
    </>
  );
}
