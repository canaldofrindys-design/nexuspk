/** Brasão original do NexusPK (escudo, cristal e lâminas), desenhado em SVG. */
export function Crest({ className = "size-9" }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden>
      <defs>
        <linearGradient id="crest-gold" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#f3d892" />
          <stop offset="1" stopColor="#8a6a3a" />
        </linearGradient>
        <linearGradient id="crest-crystal" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#46e4f0" />
          <stop offset="1" stopColor="#5c6dff" />
        </linearGradient>
      </defs>
      <path d="M8 6 L40 6 L40 24 C40 34 32 41 24 44 C16 41 8 34 8 24Z" fill="#0a1020" stroke="url(#crest-gold)" strokeWidth="2" />
      <path d="M14 14 L34 34 M34 14 L14 34" stroke="url(#crest-gold)" strokeWidth="2.2" strokeLinecap="round" />
      <path d="M24 12 L29 22 L24 34 L19 22Z" fill="url(#crest-crystal)" stroke="#cfe6ff" strokeWidth="0.8" />
    </svg>
  );
}
