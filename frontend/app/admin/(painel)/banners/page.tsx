import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { RareDropsForm, RareItemsForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import { safeImage } from "@/features/content/labels";
import { formatDateTime, formatNumber } from "@/features/home/format";
import type { BannerRecord, ItemPickups, RareDropSettings, RareItemSettings, SettingView } from "@/features/site/types";
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
  const [r, drops, rare, pickups] = await Promise.all([
    apiSend<BannerRecord[]>("GET", "/api/v1/admin/banners", { token }),
    apiSend<SettingView<RareDropSettings>>("GET", "/api/v1/admin/site/drops", { token }),
    apiSend<SettingView<RareItemSettings>>("GET", "/api/v1/admin/site/rare-items", { token }),
    apiSend<ItemPickups>("GET", "/api/v1/admin/site/item-pickups", { token }),
  ]);
  const named = new Map((rare.ok ? rare.data.value.items : []).map((i) => [i.code, i.name]));
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
        <GameWindow title="Drops automáticos: itens raros">
          <p className="mb-3 text-sm text-silver/70">
            O portal lê o log de itens do jogo (ItemLogDB). Cadastre aqui, uma vez, os códigos dos itens raros: toda vez que alguém pegar um
            deles, o drop aparece sozinho em &quot;Drops raros da semana&quot; na home, com o nome do personagem e a hora.
          </p>
          {rare.ok ? <RareItemsForm settings={rare.data.value} /> : <p className="text-sm text-data-bad">{rare.error.message}</p>}
          <h3 className="mt-5 mb-2 font-display text-sm text-gold-bright">Itens mais pegos nos últimos 30 dias</h3>
          {!pickups.ok || !pickups.data.available ? (
            <p className="text-sm text-data-bad">
              Não consegui ler o log de itens. Confira se o login de leitura do portal tem acesso ao ItemLogDB (veja docs/CONTEUDO.md).
            </p>
          ) : pickups.data.items.length === 0 ? (
            <p className="text-sm text-silver/70">Ninguém pegou item nos últimos 30 dias.</p>
          ) : (
            <div className="overflow-x-auto">
              <p className="mb-2 text-xs text-silver/60">
                Use para achar o código: veja quem pegou e a hora, confira no jogo qual item foi e copie o código para a lista acima.
              </p>
              <table className="w-full min-w-[560px] text-sm">
                <thead>
                  <tr className="text-left text-xs text-silver/60">
                    <th scope="col" className="py-1 font-normal">Código</th>
                    <th scope="col" className="py-1 font-normal">Na lista como</th>
                    <th scope="col" className="py-1 text-right font-normal">Vezes</th>
                    <th scope="col" className="py-1 pl-4 font-normal">Último a pegar</th>
                    <th scope="col" className="py-1 text-right font-normal">Quando</th>
                  </tr>
                </thead>
                <tbody>
                  {pickups.data.items.map((p) => (
                    <tr key={p.itemCode} className="border-t border-steel/60">
                      <td className="tabular py-1 text-silver">{p.itemCode}</td>
                      <td className={`py-1 ${named.has(p.itemCode) ? "text-data-ok" : "text-silver/40"}`}>{named.get(p.itemCode) ?? "não é raro"}</td>
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
            Opcional: para um drop que não sai no log ou para dizer de qual boss caiu. Aparece junto com os automáticos na home.
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
