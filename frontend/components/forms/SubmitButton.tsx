"use client";

import { LoaderCircle } from "lucide-react";
import { useFormStatus } from "react-dom";
import type { ReactNode } from "react";

export function SubmitButton({ children, variant = "gold" }: { children: ReactNode; variant?: "gold" | "arcane" }) {
  const { pending } = useFormStatus();
  const color =
    variant === "gold"
      ? "bg-gradient-to-b from-gold-bright to-gold border-gold-bright/60"
      : "bg-gradient-to-b from-arcane to-[#2f7fd1] border-arcane/60";
  return (
    <button
      type="submit"
      disabled={pending}
      className={`btn-glow inline-flex w-full items-center justify-center gap-2 rounded-sm border px-4 py-2 text-sm font-semibold text-night disabled:opacity-70 ${color}`}
    >
      {pending ? <LoaderCircle className="size-4 animate-spin" aria-hidden /> : null}
      {children}
    </button>
  );
}
