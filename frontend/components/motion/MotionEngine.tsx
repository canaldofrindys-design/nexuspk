"use client";

import { usePathname } from "next/navigation";
import { useEffect } from "react";

/**
 * Motor de movimento do portal (sem dependências): entrada dos blocos ao rolar, inclinação 3D
 * dos cartões, rastreio do mouse no destaque da home e, nos navegadores sem animação ligada à
 * rolagem (animation-timeline), o cálculo do parallax e da barra de progresso em JavaScript.
 *
 * Marcações aceitas no HTML:
 * - `data-reveal="up|left|right|zoom|fade"`: entra ao aparecer na tela. Janelas (.game-window)
 *   e painéis de vidro (.glass-panel) entram sozinhos, sem marcação.
 * - `data-tilt`: inclina em 3D seguindo o ponteiro (só mouse/caneta).
 * - `data-mouse-scope`: publica --mx/--my (-1 a 1) para as camadas que seguem o mouse.
 * - `.px-scope` / `.px-layer`: parallax por CSS; aqui só o substituto para quem não tem suporte.
 *
 * Com "reduzir movimento" ligado no sistema nada disso roda e o conteúdo aparece parado.
 */

const REVEAL_SELECTOR = "main [data-reveal], main .game-window, main .glass-panel";
const STAGGER_MS = 90;
const MAX_STAGGER = 6;

function reducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/** Esconde só o que ainda está abaixo da dobra; o que já está na tela fica como veio do servidor. */
function setupReveal(): () => void {
  const io = new IntersectionObserver(
    (entries) => {
      const entering = entries
        .filter((e) => e.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top || a.boundingClientRect.left - b.boundingClientRect.left);
      entering.forEach((entry, i) => {
        const el = entry.target as HTMLElement;
        io.unobserve(el);
        el.style.setProperty("--reveal-delay", `${Math.min(i, MAX_STAGGER) * STAGGER_MS}ms`);
        el.dataset.revealState = "shown";
        // Depois da entrada, devolve o elemento ao estado normal (sem translate/opacity próprios).
        const done = () => {
          el.removeAttribute("data-reveal-state");
          el.style.removeProperty("--reveal-delay");
        };
        el.addEventListener("transitionend", done, { once: true });
        window.setTimeout(done, 1600);
      });
    },
    { rootMargin: "0px 0px -8% 0px", threshold: 0.08 },
  );

  const scan = () => {
    const vh = window.innerHeight;
    document.querySelectorAll<HTMLElement>(REVEAL_SELECTOR).forEach((el) => {
      if (el.dataset.revealBound) return;
      el.dataset.revealBound = "1";
      // Janela dentro de outro bloco que já entra: não anima de novo (a menos que peça data-reveal).
      const parent = el.parentElement?.closest<HTMLElement>("[data-reveal-bound]");
      if (parent && !el.hasAttribute("data-reveal")) return;
      if (el.getBoundingClientRect().top < vh * 0.92) return;
      el.dataset.revealState = "hidden";
      io.observe(el);
    });
  };

  scan();
  let pending = 0;
  const mo = new MutationObserver(() => {
    if (pending) return;
    pending = window.requestAnimationFrame(() => {
      pending = 0;
      scan();
    });
  });
  const main = document.querySelector("main");
  if (main) mo.observe(main, { childList: true, subtree: true });

  return () => {
    io.disconnect();
    mo.disconnect();
    if (pending) cancelAnimationFrame(pending);
    // Nada pode ficar escondido sem observador.
    document.querySelectorAll<HTMLElement>("[data-reveal-bound]").forEach((el) => {
      el.removeAttribute("data-reveal-bound");
      el.removeAttribute("data-reveal-state");
    });
  };
}

/** Inclinação 3D com brilho, delegada no documento (um só ouvinte para todos os cartões). */
function setupTilt(): () => void {
  if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return () => {};
  let current: HTMLElement | null = null;
  let frame = 0;
  let last: PointerEvent | null = null;

  const reset = (el: HTMLElement) => {
    el.style.removeProperty("--tilt-x");
    el.style.removeProperty("--tilt-y");
    el.style.removeProperty("--glare-x");
    el.style.removeProperty("--glare-y");
    el.removeAttribute("data-tilting");
  };

  const apply = () => {
    frame = 0;
    if (!current || !last) return;
    const r = current.getBoundingClientRect();
    const x = (last.clientX - r.left) / r.width;
    const y = (last.clientY - r.top) / r.height;
    current.style.setProperty("--tilt-x", `${((0.5 - y) * 14).toFixed(2)}deg`);
    current.style.setProperty("--tilt-y", `${((x - 0.5) * 16).toFixed(2)}deg`);
    current.style.setProperty("--glare-x", `${(x * 100).toFixed(1)}%`);
    current.style.setProperty("--glare-y", `${(y * 100).toFixed(1)}%`);
  };

  const onMove = (e: PointerEvent) => {
    if (e.pointerType === "touch") return;
    const el = (e.target as Element | null)?.closest<HTMLElement>("[data-tilt]") ?? null;
    if (el !== current) {
      if (current) reset(current);
      current = el;
      if (current) current.dataset.tilting = "";
    }
    last = e;
    if (current && !frame) frame = requestAnimationFrame(apply);
  };
  const onLeave = () => {
    if (current) reset(current);
    current = null;
  };

  document.addEventListener("pointermove", onMove, { passive: true });
  document.documentElement.addEventListener("pointerleave", onLeave);
  return () => {
    document.removeEventListener("pointermove", onMove);
    document.documentElement.removeEventListener("pointerleave", onLeave);
    if (frame) cancelAnimationFrame(frame);
    if (current) reset(current);
  };
}

/** Camadas que seguem o mouse, com suavização (cada quadro anda 8% do caminho). */
function setupMouseScopes(): () => void {
  if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return () => {};
  const scopes = Array.from(document.querySelectorAll<HTMLElement>("[data-mouse-scope]"));
  if (!scopes.length) return () => {};
  const state = new Map(scopes.map((s) => [s, { tx: 0, ty: 0, x: 0, y: 0 }]));
  let frame = 0;

  const tick = () => {
    frame = 0;
    let moving = false;
    state.forEach((s, el) => {
      s.x += (s.tx - s.x) * 0.08;
      s.y += (s.ty - s.y) * 0.08;
      if (Math.abs(s.tx - s.x) > 0.001 || Math.abs(s.ty - s.y) > 0.001) moving = true;
      el.style.setProperty("--mx", s.x.toFixed(4));
      el.style.setProperty("--my", s.y.toFixed(4));
    });
    if (moving) frame = requestAnimationFrame(tick);
  };

  const onMove = (e: PointerEvent) => {
    if (e.pointerType === "touch") return;
    state.forEach((s, el) => {
      const r = el.getBoundingClientRect();
      if (r.bottom < 0 || r.top > window.innerHeight) return;
      s.tx = Math.max(-1, Math.min(1, ((e.clientX - r.left) / r.width) * 2 - 1));
      s.ty = Math.max(-1, Math.min(1, ((e.clientY - r.top) / r.height) * 2 - 1));
    });
    if (!frame) frame = requestAnimationFrame(tick);
  };

  window.addEventListener("pointermove", onMove, { passive: true });
  return () => {
    window.removeEventListener("pointermove", onMove);
    if (frame) cancelAnimationFrame(frame);
  };
}

/** Substituto do parallax e da barra de progresso para navegadores sem animation-timeline. */
function setupScrollFallback(): () => void {
  const root = document.documentElement;
  let frame = 0;
  const update = () => {
    frame = 0;
    const vh = window.innerHeight;
    const max = Math.max(1, root.scrollHeight - vh);
    root.style.setProperty("--scroll-p", (window.scrollY / max).toFixed(4));
    root.style.setProperty("--scroll-y", String(Math.round(window.scrollY)));
    document.querySelectorAll<HTMLElement>(".px-scope").forEach((scope) => {
      const r = scope.getBoundingClientRect();
      if (r.bottom < -100 || r.top > vh + 100) return;
      // Mesmo cálculo do intervalo "cover" da linha do tempo de visibilidade.
      const p = (vh - r.top) / (vh + r.height);
      scope.style.setProperty("--px-p", Math.max(0, Math.min(1, p)).toFixed(4));
    });
  };
  const onScroll = () => {
    if (!frame) frame = requestAnimationFrame(update);
  };
  update();
  window.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("resize", onScroll);
  return () => {
    window.removeEventListener("scroll", onScroll);
    window.removeEventListener("resize", onScroll);
    if (frame) cancelAnimationFrame(frame);
  };
}

export function MotionEngine() {
  const pathname = usePathname();

  useEffect(() => {
    const root = document.documentElement;
    if (reducedMotion()) {
      root.classList.remove("motion");
      return;
    }
    root.classList.add("motion");
    const sda = typeof CSS !== "undefined" && CSS.supports("animation-timeline: scroll()");
    root.classList.toggle("no-sda", !sda);

    const cleanups = [setupReveal(), setupTilt(), setupMouseScopes()];
    if (!sda) cleanups.push(setupScrollFallback());
    return () => cleanups.forEach((fn) => fn());
  }, [pathname]);

  return null;
}
