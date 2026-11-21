import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { RareDropsForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import { safeImage } from "@/features/content/labels";
import { formatDateTime, formatNumber } from "@/features/home/format";
import type { BannerRecord, ItemPickupStat, ItemPickups, RareDropSettings, SettingView } from "@/features/site/types";
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
  const [r, drops, pickups] = await Promise.all([
    apiSend<BannerRecord[]>("GET", "/api/v1/admin/banners", { token }),
    apiSend<SettingView<RareDropSettings>>("GET", "/api/v1/admin/site/drops", { token }),
    apiSend<ItemPickups>("GET", "/api/v1/admin/site/item-pickups", { token }),
  ]);
  // Mesma regra da API (RareDropFeed): fora ouro (grupo 5) e poções (grupo 4), pego no máximo 3 vezes em 30 dias.
  const isRare = (p: ItemPickupStat) => ![4, 5].includes((p.itemCode >>> 24) & 0xff) && p.times <= 3;
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
      <div className="mt-4">
        <GameWindow title="Drops automáticos">
          <p className="mb-3 text-sm text-silver/70">
            Não precisa cadastrar nada: o portal lê o log de itens do jogo (ItemLogDB) e mostra na home todo item raro que alguém pegou nos
            últimos 7 dias. Raro é o que foi pego no máximo 3 vezes nos últimos 30 dias (ouro e poções nunca contam).
          </p>
          <h3 className="mb-2 font-display text-sm text-gold-bright">Itens pegos nos últimos 30 dias</h3>
          {!pickups.ok || !pickups.data.available ? (
            <p className="text-sm text-data-bad">
              Não consegui ler o log de itens. Confira se o login de leitura do portal tem acesso ao ItemLogDB (veja docs/CONTEUDO.md).
            </p>
          ) : pickups.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Ninguém pegou item nos últimos 30 dias.</p>
          ) : (
            <div className="overflow-x-auto">
              <p className="mb-2 text-xs text-silver/60">
                Os 60 pegos mais recentemente, com quem pegou por último. Só conferência: nada aqui precisa ser preenchido.
              </p>
              {pickups.data.exactNames ? (
                <p className="mb-2 text-xs text-silver/60">
                  Nomes lidos dos arquivos do servidor do jogo ({formatNumber(pickups.data.exactNames)} itens).
                </p>
              ) : (
                <p className="mb-2 text-xs text-data-bad">
                  Não consegui ler os nomes dos itens nos arquivos do servidor (GameServer\OpenItem). Os nomes aparecem pelo tipo. Veja
                  docs/INSTALLATION.md, GameServer.ItemsFolder.
                </p>
              )}
              <table className="w-full min-w-[560px] text-sm">
                <thead>
                  <tr className="text-left text-xs text-silver/60">
                    <th scope="col" className="py-1 font-normal">Item</th>
                    <th scope="col" className="py-1 font-normal">Na home</th>
                    <th scope="col" className="py-1 text-right font-normal">Vezes</th>
                    <th scope="col" className="py-1 pl-4 font-normal">Último a pegar</th>
                    <th scope="col" className="py-1 text-right font-normal">Quando</th>
                  </tr>
                </thead>
                <tbody>
                  {[...pickups.data.items]
                    .sort((a, b) => Date.parse(b.lastUtc) - Date.parse(a.lastUtc))
                    .slice(0, 60)
                    .map((p) => (
                    <tr key={p.itemCode} className="border-t border-steel/60">
                      <td className="py-1 text-silver">
                        {p.name ?? p.itemCode}
                        <span className="tabular ml-2 text-xs text-silver/40">{p.itemCode}</span>
                      </td>
                      <td className={`py-1 ${isRare(p) ? "text-data-ok" : "text-silver/40"}`}>{isRare(p) ? "raro, aparece" : "comum"}</td>
                      <td className="tabular py-1 text-right text-data-info">{formatNumber(p.times)}</td>
                      <td className="py-1 pl-4 text-data-name">{p.lastCharacter}</td>
                      <td className="tabular py-1 text-right text-silver/60">{formatDateTime(p.lastUtc)}</td>
                    </tr>
                    ))}
                </tbody>
              </table>
            </div>
          )}
        </GameWindow>
      </div>
      <div className="mt-4">
        <GameWindow title="Drops anunciados à mão">
          <p className="mb-3 text-sm text-silver/70">
            Opcional (não precisa usar): só para dizer de qual boss caiu um drop. Aparece junto com os automáticos na home.
          </p>
          {drops.ok ? (
            <>
              <p className="mb-3 text-xs text-silver/60">
                {drops.data.updatedUtc
                  ? `Última alteração em ${formatDateTime(drops.data.updatedUtc)}${drops.data.updatedBy ? ` por ${drops.data.updatedBy}` : ""}.`
                  : "Nenhum drop cadastrado ainda."}
              </p>
              <RareDropsForm settings={drops.data.value} />
            </>
          ) : (
            <p className="text-sm text-data-bad">{drops.error.message}</p>
          )}
        </GameWindow>
      </div>
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
