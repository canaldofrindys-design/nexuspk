import Image from "next/image";
import { patentOf, patentSrc } from "./patents";

/** Emblema da patente PvP do jogo (sem patente, nada). withName: mostra o nome ao lado. */
export function PatentBadge({ level, size = 20, withName = false }: { level: number | null | undefined; size?: number; withName?: boolean }) {
  const p = patentOf(level);
  if (!p) return null;
  return (
    <span className="inline-flex items-center gap-1.5 align-middle" title={`Patente: ${p.name}`}>
      <Image src={patentSrc(p.emblem)} alt={withName ? "" : `Patente ${p.name}`} width={p.width} height={p.height} unoptimized style={{ height: size, width: "auto" }} />
      {withName ? <span>{p.name}</span> : null}
    </span>
  );
}
