"use client";

import { motion, useReducedMotion, useScroll, useTransform } from "framer-motion";
import { useRef, type ReactNode } from "react";
import { ParticleField } from "./ParticleField";

/**
 * Banner cinematográfico com camadas em parallax.
 * Toda a arte é original, desenhada em SVG: céu, montanhas, castelo, cristais e névoa.
 */
export function HeroScene({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const reduce = useReducedMotion();
  const { scrollYProgress } = useScroll({ target: ref, offset: ["start start", "end start"] });
  const far = useTransform(scrollYProgress, [0, 1], [0, reduce ? 0 : 60]);
  const mid = useTransform(scrollYProgress, [0, 1], [0, reduce ? 0 : 120]);
  const near = useTransform(scrollYProgress, [0, 1], [0, reduce ? 0 : 200]);

  return (
    <div ref={ref} className="relative isolate h-[min(78vh,640px)] min-h-[460px] overflow-hidden border-b border-bronze/60">
      <svg aria-hidden className="absolute inset-0 size-full" preserveAspectRatio="xMidYMid slice" viewBox="0 0 1440 640">
        <defs>
          <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#03050b" />
            <stop offset="0.55" stopColor="#0d1a36" />
            <stop offset="1" stopColor="#1b2d55" />
          </linearGradient>
          <radialGradient id="moon" cx="0.72" cy="0.22" r="0.25">
            <stop offset="0" stopColor="#cfe6ff" stopOpacity="0.55" />
            <stop offset="0.25" stopColor="#5cb8ff" stopOpacity="0.18" />
            <stop offset="1" stopColor="#5cb8ff" stopOpacity="0" />
          </radialGradient>
        </defs>
        <rect width="1440" height="640" fill="url(#sky)" />
        <rect width="1440" height="640" fill="url(#moon)" />
        <circle cx="1037" cy="140" r="34" fill="#dfeeff" opacity="0.85" />
        {[...Array(60)].map((_, i) => (
          <circle key={i} cx={(i * 263) % 1440} cy={(i * 97) % 300} r={i % 7 === 0 ? 1.4 : 0.8} fill="#cfe6ff" opacity={0.25 + (i % 5) * 0.1} />
        ))}
      </svg>

      <motion.svg aria-hidden style={{ y: far }} className="absolute inset-x-0 bottom-0 h-[75%] w-full" preserveAspectRatio="xMaxYMax slice" viewBox="0 0 1440 480">
        <path d="M0 330 L140 230 L260 300 L400 170 L520 280 L660 200 L800 300 L960 160 L1100 290 L1240 210 L1440 300 L1440 480 L0 480Z" fill="#101c38" />
      </motion.svg>

      <motion.svg aria-hidden style={{ y: mid }} className="absolute inset-x-0 bottom-0 h-[70%] w-full" preserveAspectRatio="xMaxYMax slice" viewBox="0 0 1440 440">
        {/* Penhasco com castelo */}
        <path d="M820 440 L860 300 L900 270 L1180 270 L1220 300 L1260 440Z" fill="#0b142a" />
        <g fill="#0b142a">
          <rect x="920" y="170" width="44" height="110" />
          <rect x="1100" y="150" width="50" height="130" />
          <rect x="980" y="200" width="110" height="80" />
          <rect x="1010" y="110" width="36" height="100" />
          <path d="M916 170 L942 120 L968 170Z" />
          <path d="M1096 150 L1125 92 L1154 150Z" />
          <path d="M1006 110 L1028 56 L1050 110Z" />
        </g>
        <g fill="#f3d892" opacity="0.8">
          <rect x="937" y="200" width="6" height="10" />
          <rect x="1120" y="190" width="6" height="10" />
          <rect x="1025" y="150" width="5" height="9" />
          <rect x="1030" y="235" width="6" height="10" />
        </g>
        <path d="M0 360 C200 320 360 380 560 350 C700 330 760 380 860 370 L860 440 L0 440Z" fill="#0b142a" />
      </motion.svg>

      <motion.svg aria-hidden style={{ y: near }} className="absolute inset-x-0 bottom-0 h-[55%] w-full" preserveAspectRatio="xMaxYMax slice" viewBox="0 0 1440 360">
        <defs>
          <linearGradient id="crystal" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#46e4f0" />
            <stop offset="1" stopColor="#9b6dff" stopOpacity="0.3" />
          </linearGradient>
          <filter id="glow">
            <feGaussianBlur stdDeviation="6" />
          </filter>
        </defs>
        <g filter="url(#glow)" opacity="0.7">
          <path d="M840 262 L862 182 L884 262Z" fill="#46e4f0" />
          <path d="M1290 270 L1306 205 L1322 270Z" fill="#9b6dff" />
        </g>
        <path d="M840 262 L862 182 L884 262Z" fill="url(#crystal)" />
        <path d="M874 266 L888 216 L902 266Z" fill="url(#crystal)" />
        <path d="M1290 270 L1306 205 L1322 270Z" fill="url(#crystal)" />
        {/* Ruínas e árvores antigas em primeiro plano */}
        <g fill="#05080f">
          <path d="M0 300 C180 250 320 290 480 270 C640 250 780 300 980 280 C1160 262 1300 300 1440 280 L1440 360 L0 360Z" />
          <path d="M1380 120 C1360 170 1370 230 1390 300 L1410 300 C1420 230 1430 170 1415 120Z" />
          <circle cx="1398" cy="110" r="46" />
          <circle cx="1352" cy="140" r="34" />
        </g>
      </motion.svg>

      <div aria-hidden className="absolute inset-x-0 bottom-0 h-40 bg-gradient-to-t from-night via-night/70 to-transparent" />
      <div aria-hidden className="absolute inset-y-0 left-0 w-full bg-gradient-to-r from-night/80 via-night/30 to-transparent md:w-2/3" />
      <ParticleField />

      <div className="relative z-10 mx-auto flex h-full max-w-7xl flex-col justify-end px-4 pb-14">{children}</div>
    </div>
  );
}
