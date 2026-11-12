import Image from "next/image";
import { classByCode } from "@/features/home/classes";

type Props = {
  code: number;
  /** Nome vindo da API; cai no nome local se vier vazio. */
  name?: string;
  /** Lado do ícone em pixels. */
  size?: number;
  /** Esconde o texto (o nome continua no `title` e para leitores de tela). */
  iconOnly?: boolean;
  className?: string;
};

/** Retrato da classe (ícone do jogo) com o nome ao lado. Código sem ícone mostra a inicial. */
export function ClassIcon({ code, name, size = 24 }: { code: number; name: string; size?: number }) {
  const cls = classByCode(code);
  const frame = "shrink-0 rounded-sm border border-gold/50 shadow-[0_0_6px_rgb(0_0_0/0.6)]";
  if (!cls) {
    return (
      <span
        aria-hidden
        style={{ width: size, height: size, fontSize: size * 0.55 }}
        className={`${frame} grid place-items-center bg-night/70 font-display leading-none text-gold/80`}
      >
        {name.slice(0, 1) || "?"}
      </span>
    );
  }
  return <Image src={cls.icon} alt="" width={84} height={84} style={{ width: size, height: size }} className={frame} />;
}

export function ClassBadge({ code, name, size = 24, iconOnly = false, className = "" }: Props) {
  const label = name || classByCode(code)?.name || "Desconhecida";
  return (
    <span className={`inline-flex items-center gap-2 align-middle ${className}`} title={label}>
      <ClassIcon code={code} name={label} size={size} />
      <span className={iconOnly ? "sr-only" : undefined}>{label}</span>
    </span>
  );
}
