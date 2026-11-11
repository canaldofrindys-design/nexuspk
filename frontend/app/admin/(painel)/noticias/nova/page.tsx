import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { NewsForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";

export const metadata: Metadata = { title: "Nova notícia" };

async function Body() {
  await connection();
  await requireStaff("ManageContent");
  return <NewsForm />;
}

export default function NewNewsPage() {
  return (
    <GameWindow title="Nova notícia">
      <Suspense fallback={null}>
        <Body />
      </Suspense>
    </GameWindow>
  );
}
