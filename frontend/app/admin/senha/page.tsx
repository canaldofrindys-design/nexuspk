import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import { GameWindow } from "@/components/ui/GameWindow";
import { StaffPasswordForm } from "@/features/admin/forms";
import { getStaff } from "@/features/admin/session";

export const metadata: Metadata = { title: "Trocar senha" };

async function Body() {
  await connection();
  const me = await getStaff();
  if (!me) redirect("/admin/entrar");
  return (
    <>
      <StaffPasswordForm forced={me.mustChangePassword} />
      {!me.mustChangePassword ? (
        <Link href="/admin" className="mt-3 inline-block text-sm text-arcane hover:underline">
          Voltar ao painel
        </Link>
      ) : null}
    </>
  );
}

export default function StaffPasswordPage() {
  return (
    <div className="mx-auto w-full max-w-md px-4 py-16">
      <GameWindow title="Trocar senha">
        <Suspense fallback={null}>
          <Body />
        </Suspense>
      </GameWindow>
    </div>
  );
}
