import type { ReactNode } from "react";
import { GameWindow } from "@/components/ui/GameWindow";

/** Moldura das páginas de conta: uma janela central, sem distrações. */
export function AuthPanel({ title, children, aside }: { title: string; children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="mx-auto grid w-full max-w-4xl gap-4 px-4 py-10 md:grid-cols-[1fr_280px]">
      <GameWindow title={title}>{children}</GameWindow>
      {aside ? <div className="flex flex-col gap-4">{aside}</div> : null}
    </div>
  );
}
