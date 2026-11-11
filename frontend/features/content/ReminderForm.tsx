"use client";

import { useActionState } from "react";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { initialFormState } from "@/features/auth/types";
import { reminderAction } from "./actions";

export function ReminderForm({ slug, active }: { slug: string; active: boolean }) {
  const [state, action] = useActionState(reminderAction, initialFormState);
  // Depois de uma troca bem-sucedida a página é revalidada e `active` chega atualizado.
  return (
    <form action={action} className="flex flex-col gap-2">
      <input type="hidden" name="slug" value={slug} />
      <input type="hidden" name="enable" value={active ? "0" : "1"} />
      <FormMessage state={state} />
      <SubmitButton variant={active ? "arcane" : "gold"}>{active ? "Desativar lembrete por e-mail" : "Avisar por e-mail antes do início"}</SubmitButton>
    </form>
  );
}
