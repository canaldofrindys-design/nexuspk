import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { DownloadsForm, ServerSettingsForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import { formatDateTime } from "@/features/home/format";
import type { DownloadSettings, ServerSettings, SettingView } from "@/features/site/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Conteúdo do site" };

function Updated({ view }: { view: SettingView<unknown> }) {
  return (
    <p className="mb-3 text-xs text-silver/60">
      {view.updatedUtc ? `Última alteração em ${formatDateTime(view.updatedUtc)}${view.updatedBy ? ` por ${view.updatedBy}` : ""}.` : "Ainda com os valores da instalação."}
    </p>
  );
}

async function Body() {
  await connection();
  const { token } = await requireStaff("ManageSite");
  const [server, downloads] = await Promise.all([
    apiSend<SettingView<ServerSettings>>("GET", "/api/v1/admin/site/server", { token }),
    apiSend<SettingView<DownloadSettings>>("GET", "/api/v1/admin/site/downloads", { token }),
  ]);
  return (
    <div className="flex flex-col gap-4">
      <GameWindow
        title="Informações do servidor"
        actions={
          <Link href="/servidor" className="text-xs text-arcane hover:underline">
            Ver página
          </Link>
        }
      >
        {server.ok ? (
          <>
            <Updated view={server.data} />
            <ServerSettingsForm settings={server.data.value} />
          </>
        ) : (
          <p className="text-sm text-data-bad">{server.error.message}</p>
        )}
      </GameWindow>
      <GameWindow
        title="Página de download"
        actions={
          <Link href="/download" className="text-xs text-arcane hover:underline">
            Ver página
          </Link>
        }
      >
        {downloads.ok ? (
          <>
            <Updated view={downloads.data} />
            <DownloadsForm settings={downloads.data.value} />
          </>
        ) : (
          <p className="text-sm text-data-bad">{downloads.error.message}</p>
        )}
      </GameWindow>
      <p className="text-xs text-silver/60">
        Banners e destaques da home ficam em{" "}
        <Link href="/admin/banners" className="text-arcane hover:underline">
          Banners
        </Link>
        .
      </p>
    </div>
  );
}

export default function AdminSitePage() {
  return (
    <Suspense fallback={<p className="text-sm text-silver/60">Carregando…</p>}>
      <Body />
    </Suspense>
  );
}
