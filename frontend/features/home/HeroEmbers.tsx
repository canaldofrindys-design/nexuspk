"use client";

import { useEffect, useRef } from "react";

type Ember = { x: number; y: number; r: number; vy: number; vx: number; phase: number; life: number; hue: "gold" | "arcane" };

const colors = {
  gold: [255, 214, 140],
  arcane: [120, 200, 255],
} as const;

/**
 * Brasas douradas e faíscas arcanas subindo pelo cenário. Canvas leve: pausa fora da tela e
 * com a aba em segundo plano, densidade menor no celular e nada com "reduzir movimento".
 */
export function HeroEmbers() {
  const ref = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = ref.current;
    if (!canvas || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Um brilho pré-desenhado por cor; cada quadro só copia a imagem (sem gradiente por partícula).
    const sprites = Object.fromEntries(
      (Object.keys(colors) as Ember["hue"][]).map((hue) => {
        const c = document.createElement("canvas");
        c.width = c.height = 64;
        const g = c.getContext("2d")!;
        const [r, gr, b] = colors[hue];
        const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
        grad.addColorStop(0, `rgba(${r},${gr},${b},0.95)`);
        grad.addColorStop(0.3, `rgba(${r},${gr},${b},0.3)`);
        grad.addColorStop(1, `rgba(${r},${gr},${b},0)`);
        g.fillStyle = grad;
        g.fillRect(0, 0, 64, 64);
        return [hue, c];
      }),
    ) as Record<Ember["hue"], HTMLCanvasElement>;

    const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
    let w = 0;
    let h = 0;
    let embers: Ember[] = [];
    let frame = 0;
    let visible = true;
    let last = performance.now();

    const spawn = (anywhere: boolean): Ember => ({
      x: Math.random() * w,
      y: anywhere ? Math.random() * h : h + 10,
      r: 0.6 + Math.random() * 1.9,
      vy: 14 + Math.random() * 34,
      vx: -6 + Math.random() * 12,
      phase: Math.random() * Math.PI * 2,
      life: 0,
      hue: Math.random() < 0.62 ? "gold" : "arcane",
    });

    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      w = rect.width;
      h = rect.height;
      canvas.width = Math.round(w * dpr);
      canvas.height = Math.round(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const count = Math.round(Math.min(90, (w * h) / 14000));
      embers = Array.from({ length: count }, () => spawn(true));
    };

    const draw = (now: number) => {
      frame = 0;
      const dt = Math.min(0.05, (now - last) / 1000);
      last = now;
      ctx.clearRect(0, 0, w, h);
      ctx.globalCompositeOperation = "lighter";
      for (let i = 0; i < embers.length; i++) {
        const e = embers[i];
        e.life += dt;
        e.y -= e.vy * dt;
        e.x += (e.vx + Math.sin(e.life * 1.3 + e.phase) * 10) * dt;
        if (e.y < -20 || e.x < -20 || e.x > w + 20) {
          embers[i] = spawn(false);
          continue;
        }
        // Some perto do topo e pisca de leve.
        const fade = Math.min(1, e.y / (h * 0.35)) * (0.55 + 0.45 * Math.sin(e.life * 3 + e.phase));
        const size = e.r * 10;
        ctx.globalAlpha = Math.max(0, fade);
        ctx.drawImage(sprites[e.hue], e.x - size / 2, e.y - size / 2, size, size);
      }
      ctx.globalAlpha = 1;
      if (visible && !document.hidden) frame = requestAnimationFrame(draw);
    };

    const start = () => {
      if (!frame && visible && !document.hidden) {
        last = performance.now();
        frame = requestAnimationFrame(draw);
      }
    };
    const stop = () => {
      if (frame) cancelAnimationFrame(frame);
      frame = 0;
    };

    resize();
    const ro = new ResizeObserver(resize);
    ro.observe(canvas);
    const io = new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting;
      if (visible) start();
      else stop();
    });
    io.observe(canvas);
    const onVisibility = () => (document.hidden ? stop() : start());
    document.addEventListener("visibilitychange", onVisibility);
    start();

    return () => {
      stop();
      ro.disconnect();
      io.disconnect();
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, []);

  return <canvas ref={ref} aria-hidden className="pointer-events-none absolute inset-0 -z-10 size-full" />;
}
