import { Download } from "lucide-react";
import { ButtonLink } from "@/components/ui/ButtonLink";
import { GameWindow } from "@/components/ui/GameWindow";

export function DownloadWindow() {
  return (
    <GameWindow title="Download">
      <p className="mb-3 text-sm text-silver/80">Cliente completo e atualizador do NexusPK para Windows.</p>
      <ButtonLink href="/download" icon={<Download className="size-4" aria-hidden />}>
        Baixar o jogo
      </ButtonLink>
    </GameWindow>
  );
}
