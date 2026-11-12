import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { BannerForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";

export const metadata: Metadata = { title: "Novo banner" };

async function Body() {
  await connection();
  await requireStaff("ManageContent");
  return <BannerForm />;
}

export default function NewBannerPage() {
  return (
    <GameWindow title="Novo banner">
      <Suspense fallback={null}>
        <Body />
      </Suspense>
    </GameWindow>
  );
}
