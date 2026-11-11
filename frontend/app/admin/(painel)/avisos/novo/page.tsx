import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { requireStaff } from "@/features/admin/session";
import { NoticeForm } from "@/features/support/forms";

export const metadata: Metadata = { title: "Novo aviso" };

async function Gate() {
  await connection();
  await requireStaff("ManageContent");
  return <NoticeForm />;
}

export default function NewNoticePage() {
  return (
    <GameWindow title="Novo aviso">
      <Suspense fallback={null}>
        <Gate />
      </Suspense>
    </GameWindow>
  );
}
