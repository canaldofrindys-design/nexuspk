"use client";

import { motion, useReducedMotion } from "framer-motion";

/** Gerador determinístico: as partículas são iguais no servidor e no navegador (sem erro de hidratação). */
function seeded(seed: number) {
  let s = seed;
  return () => {
    s = (s * 16807) % 2147483647;
    return (s - 1) / 2147483646;
  };
}

const rand = seeded(42);
const PARTICLES = Array.from({ length: 36 }, (_, i) => ({
  id: i,
  left: rand() * 100,
  size: 1.5 + rand() * 3,
  delay: rand() * 8,
  duration: 9 + rand() * 10,
  drift: (rand() - 0.5) * 60,
  hue: rand() > 0.7 ? "var(--color-amethyst)" : rand() > 0.4 ? "var(--color-cyan-glow)" : "var(--color-gold-bright)",
}));

export function ParticleField() {
  const reduce = useReducedMotion();
  if (reduce) return null;

  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
      {PARTICLES.map((p) => (
        <motion.span
          key={p.id}
          className="absolute bottom-0 rounded-full"
          style={{
            left: `${p.left}%`,
            width: p.size,
            height: p.size,
            background: p.hue,
            boxShadow: `0 0 ${p.size * 4}px ${p.hue}`,
          }}
          initial={{ y: 0, x: 0, opacity: 0 }}
          animate={{ y: -520, x: p.drift, opacity: [0, 0.9, 0] }}
          transition={{ duration: p.duration, delay: p.delay, repeat: Infinity, ease: "easeOut" }}
        />
      ))}
    </div>
  );
}
