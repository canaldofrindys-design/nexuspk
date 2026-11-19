import type { ReactNode } from "react";
import type { SceneKey } from "@/components/scene/scenes";
import { GameWindow } from "@/components/ui/GameWindow";

/** Moldura das páginas de conta: uma janela central, sem distrações. */
/** O cenário (`scene`) aparece no fundo da página, atrás da janela. */
export function AuthPanel({ title, children, aside, scene = "vila-riacho" }: { title: string; children: ReactNode; aside?: ReactNode; scene?: SceneKey }) {
  return (
    <div data-scene={scene} className="mx-auto grid w-full max-w-4xl gap-4 px-4 py-10 md:grid-cols-[1fr_280px]">
      <GameWindow title={title}>{children}</GameWindow>
      {aside ? <div className="flex flex-col gap-4">{aside}</div> : null}
    </div>
  );
}
