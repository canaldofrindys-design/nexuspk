import "server-only";
import { apiSend } from "@/services/api";
import type { AuthOptions } from "./types";

export async function loadAuthOptions(): Promise<AuthOptions | null> {
  const result = await apiSend<AuthOptions>("GET", "/api/v1/auth/options");
  return result.ok ? result.data : null;
}
