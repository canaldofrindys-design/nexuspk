import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { formatNumber } from "@/features/home/format";
import type { CharacterProfile } from "@/features/ranking/types";
import { apiSend } from "@/services/api";

export async function generateMetadata({ params }: PageProps<"/personagens/[nome]">): Promise<Metadata> {
  const { nome } = await params;
  return { title: decodeURIComponent(nome) };
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-silver/70">{label}</dt>
      <dd className="text-right text-silver">{children}</dd>
    </>
  );
}

async function Profile({ params }: { params: Promise<{ nome: string }> }) {
  await connection();
  const name = decodeURIComponent((await params).nome);
  if (name.length === 0 || name.length > 32) notFound();
  const result = await apiSend<CharacterProfile>("GET", `/api/v1/characters/${encodeURIComponent(name)}`);
  if (!result.ok && result.status === 404) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Dados do personagem indisponíveis no momento.</p>;
  const c = result.data;
  return (
    <div className="grid gap-4 md:grid-cols-2">
      <GameWindow title={c.name}>
        <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
          <Row label="Classe">{c.className}</Row>
          <Row label="Nível">
            <span className="tabular text-data-info">{c.level}</span>
          </Row>
          <Row label="Posição no ranking de nível">
            <span className="tabular text-data-name">
              {c.levelPosition}º de {formatNumber(c.levelTotal)}
            </span>
          </Row>
          <Row label="Clã">{c.clan ?? "sem clã"}</Row>
        </dl>
      </GameWindow>
      <GameWindow title="PvP">
        {c.pvp ? (
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
            <Row label="Posição no ranking PvP">
              <span className="tabular text-data-name">{c.pvp.position}º</span>
            </Row>
            <Row label="BP total">
              <span className="tabular text-data-info">{formatNumber(c.pvp.battlePoints)}</span>
            </Row>
            <Row label="BP da semana">
              <span className="tabular text-data-info">{formatNumber(c.pvp.weeklyBattlePoints)}</span>
            </Row>
            <Row label="Abates / mortes">
              <span className="tabular text-data-info">
                {formatNumber(c.pvp.kills)} / {formatNumber(c.pvp.deaths)}
              </span>
            </Row>
            {c.pvp.isPvpKing ? <Row label="Título">Rei do PvP</Row> : null}
          </dl>
        ) : (
          <p className="text-sm text-silver/70">Sem pontos de PvP registrados.</p>
        )}
      </GameWindow>
    </div>
  );
}

export default function CharacterPage({ params }: PageProps<"/personagens/[nome]">) {
  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-10">
      <p className="mb-4 text-sm">
        <Link href="/ranking" className="text-arcane hover:underline">
          Ranking
        </Link>
        <span className="px-2 text-silver/50">/</span>
        <Link href="/personagens" className="text-arcane hover:underline">
          Personagens
        </Link>
      </p>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Profile params={params} />
      </Suspense>
    </div>
  );
}
