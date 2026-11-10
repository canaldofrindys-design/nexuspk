import type { ReactNode } from "react";

type Props = {
  title: string;
  /** Conteúdo à direita da barra de título (abas, link "ver tudo"). */
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  as?: "section" | "aside" | "div";
};

/** Janela no estilo da interface do jogo: borda de metal, linha interna e barra de título. */
export function GameWindow({ title, actions, children, className = "", as: Tag = "section" }: Props) {
  const id = `win-${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
  return (
    <Tag aria-labelledby={id} className={`game-window ${className}`}>
      <header className="game-window__title flex items-center justify-between gap-3 px-3 py-2">
        <h2 id={id} className="text-sm text-gold-bright">
          {title}
        </h2>
        {actions}
      </header>
      <div className="p-3">{children}</div>
    </Tag>
  );
}
