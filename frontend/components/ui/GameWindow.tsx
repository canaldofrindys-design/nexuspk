import type { ReactNode } from "react";
import { KitIcon, type KitIconName } from "./KitIcon";

type Props = {
  title: string;
  /** Conteúdo à direita da barra de título (abas, link "ver tudo"). */
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  as?: "section" | "aside" | "div";
  /** Ícone do kit visual ao lado do título. */
  icon?: KitIconName;
};

/** Janela no estilo da interface do jogo: borda de metal, linha interna e barra de título. */
export function GameWindow({ title, actions, children, className = "", as: Tag = "section", icon }: Props) {
  const id = `win-${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
  return (
    <Tag aria-labelledby={id} className={`game-window ${className}`}>
      <header className="game-window__title flex flex-wrap items-center justify-between gap-3 px-3 py-2">
        <h2 id={id} className="flex items-center gap-2 text-sm text-gold-bright">
          {icon ? <KitIcon name={icon} size={28} className="-my-1.5" /> : null}
          {title}
        </h2>
        {actions ? <div className="game-window__actions">{actions}</div> : null}
      </header>
      <div className="p-3">{children}</div>
    </Tag>
  );
}
