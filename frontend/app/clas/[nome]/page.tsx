import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { ClassBadge } from "@/components/ui/ClassBadge";
import { GameWindow } from "@/components/ui/GameWindow";
import { ClanEmblem } from "@/features/home/ClanEmblem";
import { formatDate, formatNumber } from "@/features/home/format";
import type { ClanProfile } from "@/features/ranking/types";
import { apiSend } from "@/services/api";

export async function generateMetadata({ params }: PageProps<"/clas/[nome]">): Promise<Metadata> {
  const name = decodeURIComponent((await params).nome);
  return { title: `Clã ${name}`, description: `Membros, pontos e resultados do clã ${name} no NexusPK.` };
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-silver/70">{label}</dt>
      <dd className="text-right text-silver">{children}</dd>
    </>
  );
}

async function Clan({ params }: { params: Promise<{ nome: string }> }) {
  await connection();
  const name = decodeURIComponent((await params).nome).trim();
  if (name.length === 0 || name.length > 32) notFound();
  const result = await apiSend<ClanProfile>("GET", `/api/v1/clans/${encodeURIComponent(name)}`);
  if (!result.ok && result.status === 404) notFound();
  if (!result.ok) return <p className="text-sm text-data-bad">Dados do clã indisponíveis no momento.</p>;
  const c = result.data;
  return (
    <div className="grid gap-4 md:grid-cols-[320px_1fr]">
      <div className="flex flex-col gap-4">
        <GameWindow title="Clã" icon="guildasBandeira">
          <div className="mb-3 flex items-center gap-3 border-b border-steel/60 pb-3">
            <ClanEmblem iconId={c.iconId} name={c.name} size={56} />
            <div>
              <h1 className="font-display text-lg text-gold-bright">{c.name}</h1>
              <p className="text-sm text-silver/80">
                {c.position}º de {c.total} no ranking de clãs
              </p>
            </div>
          </div>
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
            <Row label="Líder">
              {c.leader ? (
                <Link href={`/personagens/${encodeURIComponent(c.leader)}`} className="text-data-name hover:underline">
                  {c.leader}
                </Link>
              ) : (
                "—"
              )}
            </Row>
            <Row label="Membros">
              <span className="tabular text-data-info">{c.members}</span>
            </Row>
            <Row label="Pontos no SoD">
              <span className="tabular text-data-info">{formatNumber(c.points)}</span>
            </Row>
            <Row label="Vitórias / derrotas">
              <span className="tabular text-data-info">
                {c.wins} / {c.losses}
              </span>
            </Row>
            <Row label="BP dos membros">
              <span className="tabular text-data-info">{formatNumber(c.totalBattlePoints)}</span>
            </Row>
            <Row label="Fundado em">{c.createdUtc ? formatDate(c.createdUtc) : "—"}</Row>
          </dl>
        </GameWindow>
        <Link href="/ranking?tipo=clas" className="text-sm text-arcane hover:underline">
          Ranking de clãs
        </Link>
      </div>
      <GameWindow title="Membros" icon="guilda">
        {c.roster.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px] text-sm">
              <caption className="sr-only">Membros do clã {c.name}</caption>
              <thead>
                <tr className="text-left text-xs text-silver/60">
                  <th scope="col" className="py-1 font-normal">Personagem</th>
                  <th scope="col" className="py-1 font-normal">Classe</th>
                  <th scope="col" className="py-1 text-right font-normal">Nível</th>
                  <th scope="col" className="py-1 text-right font-normal">BP</th>
                </tr>
              </thead>
              <tbody>
                {c.roster.map((m) => (
                  <tr key={m.name} className="border-t border-steel/60">
                    <td className="py-1.5">
                      <Link href={`/personagens/${encodeURIComponent(m.name)}`} className="font-medium text-data-name hover:underline">
                        {m.name}
                      </Link>
                      {m.isLeader ? <span className="ml-2 text-xs text-gold">líder</span> : null}
                    </td>
                    <td className="py-1.5 text-silver/80">
                      <ClassBadge code={m.classCode} name={m.className} />
                    </td>
                    <td className="tabular py-1.5 text-right text-data-info">{m.level}</td>
                    <td className="tabular py-1.5 text-right text-silver/80">{m.battlePoints ? formatNumber(m.battlePoints) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-silver/70">Nenhum membro aparece no ranking público.</p>
        )}
        {c.roster.length < c.members ? (
          <p className="mt-3 text-xs text-silver/60">A lista mostra só os personagens que aparecem no ranking público; a equipe do servidor fica de fora.</p>
        ) : null}
      </GameWindow>
    </div>
  );
}

export default function ClanPage({ params }: PageProps<"/clas/[nome]">) {
  return (
    <div data-scene="vale-dourado" className="mx-auto w-full max-w-5xl px-4 py-10">
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <Clan params={params} />
      </Suspense>
    </div>
  );
}
