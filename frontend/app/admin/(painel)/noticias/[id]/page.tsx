import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { NewsForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";
import type { NewsRecord } from "@/features/admin/types";
import { apiSend } from "@/services/api";

export const metadata: Metadata = { title: "Editar notícia" };

async function Body({ params }: { params: Promise<{ id: string }> }) {
  await connection();
  const { token } = await requireStaff("ManageContent");
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  const r = await apiSend<NewsRecord>("GET", `/api/v1/admin/news/${id}`, { token });
  if (!r.ok && r.status === 404) notFound();
  if (!r.ok) return <p className="text-sm text-data-bad">{r.error.message}</p>;
  return (
    <>
      {r.data.status === "published" ? (
        <p className="mb-3 text-sm">
          <Link href={`/noticias/${r.data.slug}`} className="text-arcane hover:underline">
            Ver no site
          </Link>
        </p>
      ) : null}
      <NewsForm news={r.data} />
    </>
  );
}

export default function EditNewsPage({ params }: PageProps<"/admin/noticias/[id]">) {
  return (
    <GameWindow title="Editar notícia">
      <Suspense fallback={null}>
        <Body params={params} />
      </Suspense>
    </GameWindow>
  );
}
