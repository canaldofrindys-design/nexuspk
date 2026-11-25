import "server-only";
import { cache } from "react";
import { apiGet } from "@/services/api";
import type { HomeSnapshot } from "./types";

/**
 * Uma chamada por requisição, compartilhada entre os blocos da página.
 * Retorna null quando a API está fora; cada bloco se mostra como indisponível.
 * A API responde com a home já montada, mas logo depois de ligar ela ainda monta a primeira: espera até 15 segundos
 * em vez dos 5 padrão, para a página não cair nos ícones decorativos e em "Patentes indisponíveis".
 */
export const loadHome = cache(async (): Promise<HomeSnapshot | null> => {
  try {
    return await apiGet<HomeSnapshot>("/api/v1/home", { signal: AbortSignal.timeout(15000) });
  } catch {
    return null;
  }
});
