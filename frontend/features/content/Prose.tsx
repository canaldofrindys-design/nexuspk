import type { ReactNode } from "react";

/**
 * Texto cadastrado pela equipe. Formato simples, sem HTML:
 * linha em branco separa parágrafos, "## " abre um subtítulo e linhas com "- " viram lista.
 * Tudo é renderizado como texto pelo React, então nada do conteúdo vira marcação.
 */
export function Prose({ text }: { text: string }) {
  const blocks = text.replace(/\r\n/g, "\n").split(/\n{2,}/).map((b) => b.trim()).filter(Boolean);
  const out: ReactNode[] = [];
  blocks.forEach((block, i) => {
    const lines = block.split("\n");
    if (lines.length === 1 && block.startsWith("## ")) {
      out.push(
        <h3 key={i} className="mt-2 text-base font-semibold text-gold">
          {block.slice(3)}
        </h3>,
      );
    } else if (lines.every((l) => l.startsWith("- "))) {
      out.push(
        <ul key={i} className="list-disc space-y-1 pl-5">
          {lines.map((l, j) => (
            <li key={j}>{l.slice(2)}</li>
          ))}
        </ul>,
      );
    } else {
      out.push(
        <p key={i} className="whitespace-pre-line">
          {block}
        </p>,
      );
    }
  });
  return <div className="flex flex-col gap-3 text-sm leading-relaxed text-silver">{out}</div>;
}
