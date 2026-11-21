import Link from "next/link";
import { ClassBadge } from "@/components/ui/ClassBadge";
import { formatDateTime, formatNumber } from "@/features/home/format";
import type { PanelCharacter } from "./types";

/*
 * Ficha completa de um personagem no painel: nível e barra de experiência, mapa, clã, atributos, combate,
 * resistências e o equipamento vestido (com a foto e o link para o guia de cada item).
 * Tudo vem do arquivo do personagem no servidor, gravado quando ele salva ou sai do jogo.
 */

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b border-steel/40 py-1" title={hint}>
      <dt className="text-silver/70">{label}</dt>
      <dd className="tabular text-right text-data-info">{value}</dd>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h4 className="mb-1 text-xs font-semibold tracking-wide text-gold uppercase">{title}</h4>
      <dl className="text-sm">{children}</dl>
    </section>
  );
}

const n = formatNumber;

export function CharacterSheetCard({ character }: { character: PanelCharacter }) {
  const s = character.sheet;
  if (!s) return null;
  const pct = s.experiencePercent;
  return (
    <article className="rounded-sm border border-steel/70 bg-abyss/60 p-4" aria-labelledby={`ficha-${s.name}`}>
      <header className="flex flex-wrap items-center gap-x-4 gap-y-1">
        <h3 id={`ficha-${s.name}`} className="text-lg font-semibold text-data-name">
          {s.name}
        </h3>
        <ClassBadge code={character.classCode} name={character.className} />
        <span className="text-sm text-gold-bright">Nível {s.level}</span>
        <span className="text-sm text-silver/80">Clã: {character.clan ?? "sem clã"}</span>
        {character.rankPosition ? (
          <Link href={`/personagens/${encodeURIComponent(s.name)}`} className="text-sm text-arcane hover:underline">
            {n(character.rankPosition)}º no ranking
          </Link>
        ) : null}
      </header>

      <div className="mt-3">
        <div className="flex justify-between text-xs text-silver/70">
          <span>Experiência</span>
          <span className="tabular">{pct === null ? "nível máximo" : `${pct.toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`}</span>
        </div>
        <div
          className="mt-1 h-2.5 overflow-hidden rounded-full bg-steel/60"
          role="progressbar"
          aria-label={`Experiência de ${s.name}`}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={pct ?? 100}
        >
          <div className="h-full bg-gradient-to-r from-gold to-gold-bright" style={{ width: `${pct ?? 100}%` }} />
        </div>
        <p className="mt-1 text-xs text-silver/60">
          <span className="text-silver/80">Mapa: {s.mapName ?? "desconhecido"}</span> · salvo pelo servidor em {formatDateTime(s.savedUtc)}
        </p>
      </div>

      <div className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Section title="Atributos">
          <Stat label="Força" value={n(s.strength)} />
          <Stat label="Espírito" value={n(s.spirit)} />
          <Stat label="Talento" value={n(s.talent)} />
          <Stat label="Agilidade" value={n(s.agility)} />
          <Stat label="Vitalidade" value={n(s.health)} />
          <Stat label="Pontos livres" value={n(s.statPoints)} />
        </Section>
        <Section title="Combate">
          <Stat label="Dano" value={`${n(s.damageMin)} – ${n(s.damageMax)}`} />
          <Stat label="Taxa de ataque" value={n(s.attackRating)} />
          <Stat label="Velocidade de ataque" value={n(s.attackSpeed)} />
          <Stat label="Crítico" value={`${n(s.critical)}%`} />
          <Stat label="Defesa" value={n(s.defense)} />
          <Stat label="Bloqueio" value={`${n(s.block)}%`} />
          <Stat label="Absorção" value={n(s.absorption)} />
        </Section>
        <Section title="Geral">
          <Stat label="Mana" value={n(s.mana)} />
          <Stat label="Estamina" value={n(s.stamina)} />
          <Stat label="Velocidade" value={n(s.moveSpeed)} />
          <Stat label="Peso" value={`${n(s.weight)} / ${n(s.maxWeight)}`} />
          <Stat label="Ouro" value={n(s.gold)} />
          <Stat label="Res. orgânica" value={n(s.resistances.organic)} />
          <Stat label="Res. fogo / gelo" value={`${n(s.resistances.fire)} / ${n(s.resistances.ice)}`} />
          <Stat label="Res. raio / veneno" value={`${n(s.resistances.lightning)} / ${n(s.resistances.poison)}`} />
        </Section>
      </div>

      <section className="mt-4">
        <h4 className="mb-2 text-xs font-semibold tracking-wide text-gold uppercase">Equipamento atual</h4>
        {s.equipment.length === 0 ? (
          <p className="text-sm text-silver/60">Nada equipado.</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {s.equipment.map((e) => {
              const body = (
                <>
                  <span className="grid size-11 shrink-0 place-items-center rounded-sm border border-steel/60 bg-night/70">
                    {e.fileId ? (
                      // eslint-disable-next-line @next/next/no-img-element -- ícone .bmp do cliente do jogo, servido como está
                      <img src={`/item-imagem/${e.fileId}`} alt="" loading="lazy" className="max-h-10 max-w-10 object-contain" />
                    ) : null}
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[11px] text-silver/60">{e.slotName}</span>
                    <span className="block truncate text-sm text-silver">{e.name}</span>
                  </span>
                </>
              );
              return (
                <li key={e.slot}>
                  {e.fileId ? (
                    <Link href={`/itens/${e.fileId}`} className="flex items-center gap-2 rounded-sm p-1 hover:bg-steel/30" title={`Ver ${e.name}`}>
                      {body}
                    </Link>
                  ) : (
                    <div className="flex items-center gap-2 p-1">{body}</div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </article>
  );
}
