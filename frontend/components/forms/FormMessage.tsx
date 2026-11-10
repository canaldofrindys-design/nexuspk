import type { FormState } from "@/features/auth/types";

export function FormMessage({ state }: { state: FormState }) {
  if (state.status === "idle" || !state.message) return null;
  const tone = state.status === "success" ? "border-data-ok/60 text-data-ok" : "border-data-bad/60 text-data-bad";
  return (
    <p role={state.status === "error" ? "alert" : "status"} className={`rounded-sm border bg-night/70 px-3 py-2 text-sm ${tone}`}>
      {state.message}
    </p>
  );
}
