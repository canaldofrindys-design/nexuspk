import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { EventForm } from "@/features/admin/forms";
import { requireStaff } from "@/features/admin/session";

export const metadata: Metadata = { title: "Novo evento" };

async function Body() {
  await connection();
  await requireStaff("ManageContent");
  return <EventForm />;
}

export default function NewEventPage() {
  return (
    <GameWindow title="Novo evento">
      <Suspense fallback={null}>
        <Body />
      </Suspense>
    </GameWindow>
  );
}
