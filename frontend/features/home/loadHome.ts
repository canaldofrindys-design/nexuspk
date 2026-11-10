import "server-only";
import { cache } from "react";
import { apiGet } from "@/services/api";
import type { HomeSnapshot } from "./types";

/**
 * Uma chamada por requisição, compartilhada entre os blocos da página.
 * Retorna null quando a API está fora; cada bloco se mostra como indisponível.
 */
export const loadHome = cache(async (): Promise<HomeSnapshot | null> => {
  try {
    return await apiGet<HomeSnapshot>("/api/v1/home");
  } catch {
    return null;
  }
});
