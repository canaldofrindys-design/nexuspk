"use client";

import { Bell, BellOff, BellRing } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { KitIcon } from "@/components/ui/KitIcon";
import { formatCountdown } from "@/features/home/Countdown";
import { describeWeekdays, eventTimes, scheduleKinds, upcomingOccurrences } from "@/features/home/eventSchedule";
import type { ScheduledEvent } from "@/features/site/types";

/**
 * Horários de boss (e dos outros eventos fixos) com contagem regressiva e aviso no navegador.
 *
 * - O jogador marca quais bosses quer acompanhar e quantos minutos antes quer o aviso. A escolha fica
 *   no próprio navegador (localStorage), sem conta e sem servidor de push.
 * - O aviso usa a Notification API e só dispara com a página aberta em alguma aba (pode estar em segundo
 *   plano). Sem permissão, o aviso aparece na própria página.
 * - Horários em Brasília (UTC−3); a contagem usa o relógio do aparelho.
 */

type Prefs = { watching: string[]; lead: number };

const PREFS_KEY = "nexuspk:bosses:avisos";
const SENT_KEY = "nexuspk:bosses:enviados";
const LEADS = [1, 5, 10, 15, 30] as const;
const DEFAULT_PREFS: Prefs = { watching: [], lead: 10 };

const brt = new Intl.DateTimeFormat("pt-BR", { timeZone: "America/Sao_Paulo", weekday: "short", hour: "2-digit", minute: "2-digit" });
const brtTime = new Intl.DateTimeFormat("pt-BR", { timeZone: "America/Sao_Paulo", hour: "2-digit", minute: "2-digit" });

/** Chave estável de um evento fixo: nome + tipo (o painel não tem id). */
export function eventKey(ev: Pick<ScheduledEvent, "name" | "kind">) {
  return `${ev.kind}:${ev.name.trim().toLowerCase()}`;
}

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = window.localStorage.getItem(key);
    return raw ? ({ ...fallback, ...(JSON.parse(raw) as T) } as T) : fallback;
  } catch {
    return fallback;
  }
}

function writeJson(key: string, value: unknown) {
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Navegador sem armazenamento (aba anônima, bloqueio): o aviso vale só enquanto a página estiver aberta.
  }
}

type Permission = NotificationPermission | "unsupported";

function currentPermission(): Permission {
  return typeof window !== "undefined" && "Notification" in window ? Notification.permission : "unsupported";
}

type Alert = { key: string; title: string; body: string };

export function BossTimers({ bosses, others }: { bosses: ScheduledEvent[]; others: ScheduledEvent[] }) {
  const [now, setNow] = useState<number | null>(null);
  const [prefs, setPrefs] = useState<Prefs>(DEFAULT_PREFS);
  const [permission, setPermission] = useState<Permission>("default");
  const [alert, setAlert] = useState<Alert | null>(null);
  const sent = useRef<Record<string, number>>({});
  const all = useMemo(() => [...bosses, ...others], [bosses, others]);

  const prefsRef = useRef<Prefs>(DEFAULT_PREFS);
  const allRef = useRef<ScheduledEvent[]>(all);
  useEffect(() => {
    allRef.current = all;
  }, [all]);

  // Dispara o aviso: uma vez por horário, entre "lead" minutos antes e o início.
  const checkAlerts = useCallback((at: number) => {
    const { watching, lead } = prefsRef.current;
    if (watching.length === 0) return;
    for (const ev of allRef.current) {
      const key = eventKey(ev);
      if (!watching.includes(key)) continue;
      const next = upcomingOccurrences(ev, at, 1)[0];
      if (!next || next.start <= at || next.start - lead * 60_000 > at) continue;
      const id = `${key}@${next.start}`;
      if (sent.current[id]) continue;
      sent.current[id] = next.start;
      // Guarda só os avisos das últimas 24 h.
      for (const [k, when] of Object.entries(sent.current)) if (when < at - 86400_000) delete sent.current[k];
      writeJson(SENT_KEY, sent.current);
      const minutes = Math.max(1, Math.round((next.start - at) / 60_000));
      const title = `${ev.name} em ${minutes} min`;
      const body = [ev.place ? `Local: ${ev.place}.` : "", `Começa às ${brt.format(next.start)} (Brasília).`].filter(Boolean).join(" ");
      setAlert({ key: id, title, body });
      if (currentPermission() === "granted") {
        try {
          new Notification(title, { body, tag: id, icon: "/brand/logo-nexuspk.webp" });
        } catch {
          // Android Chrome só aceita notificação por service worker: fica o aviso na página.
        }
      }
    }
  }, []);

  // Lê as escolhas do navegador depois de montar (o servidor não conhece o localStorage) e marca o relógio a cada segundo.
  useEffect(() => {
    const tick = () => {
      const at = Date.now();
      setNow(at);
      checkAlerts(at);
    };
    const start = window.setTimeout(() => {
      prefsRef.current = readJson(PREFS_KEY, DEFAULT_PREFS);
      sent.current = readJson<Record<string, number>>(SENT_KEY, {});
      setPrefs(prefsRef.current);
      setPermission(currentPermission());
      tick();
    }, 0);
    const id = window.setInterval(tick, 1000);
    return () => {
      window.clearTimeout(start);
      window.clearInterval(id);
    };
  }, [checkAlerts]);

  const save = useCallback((next: Prefs) => {
    prefsRef.current = next;
    setPrefs(next);
    writeJson(PREFS_KEY, next);
  }, []);

  const askPermission = useCallback(async () => {
    if (currentPermission() === "unsupported") return "unsupported" as const;
    if (Notification.permission === "default") {
      try {
        await Notification.requestPermission();
      } catch {
        // Safari antigo usa callback; o estado é lido abaixo.
      }
    }
    const p = currentPermission();
    setPermission(p);
    return p;
  }, []);

  const toggle = useCallback(
    async (key: string) => {
      const on = prefs.watching.includes(key);
      save({ ...prefs, watching: on ? prefs.watching.filter((k) => k !== key) : [...prefs.watching, key] });
      if (!on) await askPermission();
    },
    [prefs, save, askPermission],
  );

  const toggleAll = useCallback(async () => {
    const keys = bosses.map(eventKey);
    const allOn = keys.length > 0 && keys.every((k) => prefs.watching.includes(k));
    save({ ...prefs, watching: allOn ? prefs.watching.filter((k) => !keys.includes(k)) : [...new Set([...prefs.watching, ...keys])] });
    if (!allOn) await askPermission();
  }, [bosses, prefs, save, askPermission]);

  const testNotification = useCallback(async () => {
    const p = await askPermission();
    const title = "Aviso de boss ligado";
    const body = `Você vai receber o aviso ${prefs.lead} min antes de cada boss marcado, com esta página aberta.`;
    setAlert({ key: `teste-${Date.now()}`, title, body });
    if (p === "granted") {
      try {
        new Notification(title, { body, icon: "/brand/logo-nexuspk.webp" });
      } catch {
        // Ver acima.
      }
    }
  }, [askPermission, prefs.lead]);

  const allOn = bosses.length > 0 && bosses.every((b) => prefs.watching.includes(eventKey(b)));
  const watchingCount = all.filter((e) => prefs.watching.includes(eventKey(e))).length;

  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="avisos-title" className="game-window px-4 py-4">
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div className="min-w-0">
            <h2 id="avisos-title" className="flex items-center gap-2 font-display text-lg font-bold text-gold-bright">
              <BellRing aria-hidden className="size-5" /> Aviso no navegador
            </h2>
            <p className="mt-1 text-sm text-silver/80">
              Marque os bosses que você quer acompanhar e deixe esta página aberta numa aba (pode ficar em segundo plano). O navegador avisa antes de cada horário.
            </p>
            <PermissionNote permission={permission} watching={watchingCount} />
          </div>
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <label className="flex items-center gap-2 text-sm text-silver/90">
              Avisar
              <select
                value={prefs.lead}
                onChange={(e) => save({ ...prefs, lead: Number(e.target.value) })}
                className="rounded-sm border border-bronze/70 bg-night/80 px-2 py-1.5 text-sm text-silver"
              >
                {LEADS.map((m) => (
                  <option key={m} value={m}>
                    {m} min antes
                  </option>
                ))}
              </select>
            </label>
            {bosses.length > 0 ? (
              <button type="button" onClick={toggleAll} className="border border-gold bg-gold/15 px-3 py-1.5 text-sm text-gold-bright hover:bg-gold/25">
                {allOn ? "Parar avisos dos bosses" : "Avisar de todos os bosses"}
              </button>
            ) : null}
            <button type="button" onClick={testNotification} className="border border-steel px-3 py-1.5 text-sm text-silver hover:border-arcane hover:text-arcane">
              Testar aviso
            </button>
          </div>
        </div>
        {alert ? (
          <div role="status" className="mt-4 flex items-start justify-between gap-3 border border-gold/70 bg-gold/10 px-3 py-2">
            <p className="text-sm">
              <span className="font-semibold text-gold-bright">{alert.title}.</span> <span className="text-silver/85">{alert.body}</span>
            </p>
            <button type="button" onClick={() => setAlert(null)} className="shrink-0 text-xs text-silver/70 hover:text-gold-bright">
              Fechar
            </button>
          </div>
        ) : null}
      </section>

      <section aria-labelledby="bosses-title">
        <div className="mb-3 flex items-center gap-3">
          <h2 id="bosses-title" className="font-display text-lg font-bold tracking-wide text-gold-bright uppercase">
            Bosses
          </h2>
          <span aria-hidden className="gold-rule flex-1" />
        </div>
        {bosses.length === 0 ? (
          <p className="game-window px-4 py-4 text-sm text-silver/80">
            A equipe ainda está publicando os horários dos bosses. Enquanto isso, os eventos com boss da semana ficam na agenda de eventos.
          </p>
        ) : (
          <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {sortByNext(bosses, now).map((ev) => (
              <EventCard key={eventKey(ev)} ev={ev} now={now} watching={prefs.watching.includes(eventKey(ev))} onToggle={toggle} />
            ))}
          </ul>
        )}
      </section>

      {others.length > 0 ? (
        <section aria-labelledby="fixos-title">
          <div className="mb-3 flex items-center gap-3">
            <h2 id="fixos-title" className="font-display text-lg font-bold tracking-wide text-gold-bright uppercase">
              Guerras e eventos fixos
            </h2>
            <span aria-hidden className="gold-rule flex-1" />
          </div>
          <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {sortByNext(others, now).map((ev) => (
              <EventCard key={eventKey(ev)} ev={ev} now={now} watching={prefs.watching.includes(eventKey(ev))} onToggle={toggle} />
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}

/** Ordena pelo próximo horário (o que está acontecendo vem primeiro). Antes de montar, mantém a ordem do painel. */
function sortByNext(list: ScheduledEvent[], now: number | null) {
  if (now === null) return list;
  const next = (e: ScheduledEvent) => upcomingOccurrences(e, now, 1)[0]?.start ?? Number.MAX_SAFE_INTEGER;
  return [...list].sort((a, b) => next(a) - next(b));
}

function PermissionNote({ permission, watching }: { permission: Permission; watching: number }) {
  const text =
    permission === "unsupported"
      ? "Este navegador não mostra notificações: o aviso aparece aqui na página."
      : permission === "denied"
        ? "As notificações deste site estão bloqueadas no navegador. Libere no cadeado ao lado do endereço; até lá, o aviso aparece aqui na página."
        : permission === "granted"
          ? watching > 0
            ? `Notificações ligadas para ${watching} ${watching === 1 ? "horário" : "horários"}.`
            : "Notificações liberadas. Marque um boss para começar."
          : "Ao marcar o primeiro boss, o navegador pergunta se pode mostrar notificações.";
  return <p className="mt-1 text-xs text-silver/60">{text}</p>;
}

function EventCard({ ev, now, watching, onToggle }: { ev: ScheduledEvent; now: number | null; watching: boolean; onToggle: (key: string) => void }) {
  const kind = scheduleKinds[ev.kind] ?? scheduleKinds.evento;
  const times = eventTimes(ev);
  const next = now === null ? null : (upcomingOccurrences(ev, now, 1)[0] ?? null);
  const live = next !== null && now !== null && next.start <= now;
  const nextTime = next ? brtTime.format(next.start) : null;
  return (
    <li className={`game-window flex h-full flex-col gap-3 px-4 py-4 ${live ? "ring-1 ring-gold" : ""}`}>
      <div className="flex items-start gap-3">
        <KitIcon name={kind.icon} size={48} className="shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="font-display text-lg leading-tight font-bold text-gold-bright">{ev.name}</p>
          <p className="text-xs text-silver/70">
            {ev.kind === "boss" || kind.label === ev.name ? null : <span>{kind.label} · </span>}
            {ev.place ? <span>{ev.place} · </span> : null}
            {describeWeekdays(ev.days)}
          </p>
        </div>
        <button
          type="button"
          onClick={() => onToggle(eventKey(ev))}
          aria-pressed={watching}
          aria-label={watching ? `Parar o aviso de ${ev.name}` : `Avisar antes de ${ev.name}`}
          className={`flex shrink-0 items-center gap-1.5 border px-2.5 py-1 text-xs ${watching ? "border-gold bg-gold/20 text-gold-bright" : "border-steel text-silver/80 hover:border-arcane hover:text-arcane"}`}
        >
          {watching ? <Bell aria-hidden className="size-3.5" /> : <BellOff aria-hidden className="size-3.5" />}
          {watching ? "Avisando" : "Avisar"}
        </button>
      </div>
      {ev.description ? <p className="text-sm text-silver/80">{ev.description}</p> : null}
      <ul aria-label="Horários do dia (Brasília)" className="flex flex-wrap gap-1.5">
        {times.map((t) => (
          <li key={t} className={`tabular border px-2 py-0.5 text-xs ${t === nextTime ? "border-gold text-gold-bright" : "border-steel/70 text-silver/80"}`}>
            {t}
          </li>
        ))}
      </ul>
      <p className="mt-auto text-sm" suppressHydrationWarning>
        {now === null || next === null ? (
          <span className="text-silver/60">Horário de Brasília</span>
        ) : live ? (
          <>
            <span className="font-semibold text-data-ok">Acontecendo agora</span>
            <span className="text-silver/70"> · termina em </span>
            <span className="tabular text-silver">{formatCountdown(next.end - now)}</span>
          </>
        ) : (
          <>
            <span className="text-silver/70">Próximo: {brt.format(next.start)} · </span>
            <span className="tabular font-semibold text-data-info">{formatCountdown(next.start - now)}</span>
          </>
        )}
      </p>
    </li>
  );
}
