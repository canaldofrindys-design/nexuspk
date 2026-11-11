import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { Calendar } from "@/features/content/Calendar";
import { EventList } from "@/features/content/EventList";
import { currentYearMonth, localDay, monthName, shiftMonth } from "@/features/content/dates";
import { eventCategories, type EventCategory } from "@/features/content/labels";
import type { EventCalendar, EventOverview, EventSummary } from "@/features/content/types";
import { Tabs } from "@/features/ranking/Tabs";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Eventos", description: "Agenda e calendário de eventos do NexusPK." };

type Query = { ano?: string; mes?: string; categoria?: string };

function parse(q: Query) {
  const now = currentYearMonth();
  const y = Number(q.ano);
  const m = Number(q.mes);
  const valid = Number.isInteger(y) && y >= 2020 && y <= 2100 && Number.isInteger(m) && m >= 1 && m <= 12;
  const category = q.categoria && q.categoria in eventCategories ? (q.categoria as EventCategory) : undefined;
  return { year: valid ? y : now.year, month: valid ? m : now.month, category };
}

type State = ReturnType<typeof parse>;

function url(s: State, change: Partial<State>) {
  const n = { ...s, ...change };
  const now = currentYearMonth();
  const p = new URLSearchParams();
  if (n.year !== now.year || n.month !== now.month) {
    p.set("ano", String(n.year));
    p.set("mes", String(n.month));
  }
  if (n.category) p.set("categoria", n.category);
  const qs = p.toString();
  return qs ? `/eventos?${qs}` : "/eventos";
}

const filter = (list: EventSummary[], category?: EventCategory) => (category ? list.filter((e) => e.category === category) : list);

async function EventsBody({ searchParams }: { searchParams: Promise<Query> }) {
  await connection();
  const s = parse(await searchParams);
  const [overview, calendar] = await Promise.all([
    apiSend<EventOverview>("GET", "/api/v1/events"),
    apiSend<EventCalendar>("GET", `/api/v1/events/calendar?ano=${s.year}&mes=${s.month}`),
  ]);
  const prev = shiftMonth(s.year, s.month, -1);
  const next = shiftMonth(s.year, s.month, 1);

  return (
    <>
      <div className="mb-4">
        <Tabs
          label="Tipo de evento"
          tabs={[
            { href: url(s, { category: undefined }), label: "Todos", active: !s.category },
            ...(Object.keys(eventCategories) as EventCategory[]).map((c) => ({ href: url(s, { category: c }), label: eventCategories[c], active: s.category === c })),
          ]}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_380px]">
        <GameWindow
          title={`Calendário: ${monthName(s.year, s.month)}`}
          actions={
            <nav aria-label="Mês" className="flex gap-3 text-xs">
              <Link href={url(s, prev)} className="text-arcane hover:underline">
                Mês anterior
              </Link>
              <Link href={url(s, next)} className="text-arcane hover:underline">
                Próximo mês
              </Link>
            </nav>
          }
        >
          {calendar.ok ? (
            <>
              <Calendar year={s.year} month={s.month} events={filter(calendar.data.events, s.category)} today={localDay(new Date())} />
              <div className="mt-4 md:hidden">
                <EventList events={filter(calendar.data.events, s.category)} empty="Nenhum evento neste mês." />
              </div>
            </>
          ) : (
            <p className="text-sm text-data-bad">Calendário indisponível no momento.</p>
          )}
        </GameWindow>

        <div className="flex flex-col gap-4">
          {overview.ok ? (
            <>
              <GameWindow title="Acontecendo agora">
                <EventList events={filter(overview.data.live, s.category)} empty="Nenhum evento em andamento." />
              </GameWindow>
              <GameWindow title="Próximos eventos">
                <EventList events={filter(overview.data.upcoming, s.category)} empty="Nenhum evento agendado." />
              </GameWindow>
              <GameWindow title="Encerrados recentemente">
                <EventList events={filter(overview.data.recent, s.category)} empty="Nenhum evento encerrado ainda." />
              </GameWindow>
            </>
          ) : (
            <GameWindow title="Agenda">
              <p className="text-sm text-data-bad">Agenda indisponível no momento. Tente de novo em instantes.</p>
            </GameWindow>
          )}
        </div>
      </div>
    </>
  );
}

export default function EventsPage({ searchParams }: PageProps<"/eventos">) {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10">
      <h1 className="mb-1 font-display text-2xl text-gold-bright">Eventos</h1>
      <p className="mb-4 text-sm text-silver/70">
        Horários de Brasília. Os eventos são conduzidos pela equipe dentro do jogo; esta página é a agenda oficial.
      </p>
      <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
        <EventsBody searchParams={searchParams as Promise<Query>} />
      </Suspense>
    </div>
  );
}
