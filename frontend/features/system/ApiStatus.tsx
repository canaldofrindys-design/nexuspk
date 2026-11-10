import { connection } from "next/server";
import { apiGet } from "@/services/api";
import type { SystemInfo } from "./types";

async function loadSystemInfo(): Promise<SystemInfo | null> {
  try {
    return await apiGet<SystemInfo>("/api/v1/system/info");
  } catch {
    return null;
  }
}

/** Mostra se o frontend alcança a API. Dado real, sem valores simulados. */
export async function ApiStatus() {
  await connection();
  const info = await loadSystemInfo();

  if (!info) {
    return (
      <p className="text-sm text-silver">
        <span className="inline-block size-2 rounded-full bg-red-500 align-middle" aria-hidden /> API indisponível
      </p>
    );
  }

  return (
    <p className="text-sm text-silver">
      <span className="inline-block size-2 rounded-full bg-emerald-400 align-middle" aria-hidden /> API conectada
      <span className="text-silver/60">
        {" "}
        · versão {info.version ?? "?"} · fuso {info.timeZone}
      </span>
    </p>
  );
}
