import "server-only";
import { cache } from "react";
import { apiGet } from "@/services/api";
import type { SiteSnapshot } from "./types";

/** Conteúdo institucional (texto do servidor, downloads, redes, banners). Null quando a API está fora. */
export const loadSite = cache(async (): Promise<SiteSnapshot | null> => {
  try {
    return await apiGet<SiteSnapshot>("/api/v1/site");
  } catch {
    return null;
  }
});
