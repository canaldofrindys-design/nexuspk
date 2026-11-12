import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { safeImage } from "@/features/content/labels";
import { formatDateTime } from "@/features/home/format";
import type { BannerRecord } from "@/features/site/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Banners" };

type Q = { salvo?: string; excluido?: string };

function situation(b: BannerRecord, now: number) {
  if (!b.active) return { label: "Inativo", tone: "text-silver/60" };
  if (b.startsUtc && Date.parse(b.startsUtc) > now) return { label: "Agendado", tone: "text-data-info" };
  if (b.endsUtc && Date.parse(b.endsUtc) <= now) return { label: "Vencido", tone: "text-silver/60" };
  return { label: "Na home", tone: "text-data-ok" };
}

async function Body({ searchParams }: { searchParams: Promise<Q> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const q = await searchParams;
  const r = await apiSend<BannerRecord[]>("GET", "/api/v1/admin/banners", { token });
  const now = new Date().getTime();
  return (
    <>
      {q.salvo ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Banner salvo. A home mostra a mudança em até 1 minuto.</p> : null}
      {q.excluido ? <p className="mb-4 border border-data-ok/60 px-3 py-2 text-sm text-data-ok">Banner excluído.</p> : null}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-silver/70">Destaques do topo da página inicial. Aparecem até 5, pela ordem.</p>
        <Link href="/admin/banners/novo" className="border border-gold bg-gold/15 px-3 py-1 text-sm text-gold-bright hover:bg-gold/25">
          Novo banner
        </Link>
      </div>
      <GameWindow title="Banners">
        {!r.ok ? (
          <p className="text-sm text-data-bad">{r.error.message}</p>
        ) : r.data.length === 0 ? (
          <p className="text-sm text-silver/70">Nenhum banner. Sem banners, a home começa direto pelos avisos.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-sm">
              <thead>
                <tr className="text-left text-xs text-silver/60">
                  <th scope="col" className="py-1 font-normal">Banner</th>
                  <th scope="col" className="py-1 text-right font-normal">Ordem</th>
                  <th scope="col" className="py-1 pl-4 font-normal">Situação</th>
                  <th scope="col" className="py-1 text-right font-normal">Última edição</th>
                </tr>
              </thead>
              <tbody>
                {r.data.map((b) => {
                  const s = situation(b, now);
                  const img = safeImage(b.imageUrl);
                  return (
                    <tr key={b.id} className="border-t border-steel/60">
                      <td className="py-1.5">
                        <div className="flex items-center gap-3">
                          {img ? (
                            // eslint-disable-next-line @next/next/no-img-element -- miniatura do banner
                            <img src={img} alt="" className="h-9 w-16 border border-steel/60 object-cover" />
                          ) : (
                            <span className="h-9 w-16 border border-steel/60 bg-night" />
                          )}
                          <Link href={`/admin/banners/${b.id}`} className="text-data-name hover:underline">
                            {b.title}
                          </Link>
                        </div>
                      </td>
                      <td className="tabular py-1.5 text-right text-silver/80">{b.sortOrder}</td>
                      <td className={`py-1.5 pl-4 ${s.tone}`}>{s.label}</td>
                      <td className="tabular py-1.5 text-right text-silver/60">
                        {formatDateTime(b.updatedUtc)} {b.updatedBy ? `por ${b.updatedBy}` : ""}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </GameWindow>
    </>
  );
}

export default function AdminBanners({ searchParams }: PageProps<"/admin/banners">) {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body searchParams={searchParams as Promise<Q>} />
    </Suspense>
  );
}
