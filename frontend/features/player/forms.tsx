"use client";

import { useActionState } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { initialFormState } from "@/features/auth/types";
import { changeEmailAction, changePasswordAction, resendConfirmationAction, revokeOtherSessionsAction } from "./actions";

export function ChangePasswordForm() {
  const [state, action] = useActionState(changePasswordAction, initialFormState);
  const e = state.fieldErrors ?? {};
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <Field label="Senha atual" name="currentPassword" type="password" autoComplete="current-password" required maxLength={32} error={e.currentPassword} />
      <Field label="Nova senha" name="newPassword" type="password" autoComplete="new-password" required maxLength={32} error={e.newPassword} hint="Letras e números." />
      <Field label="Repita a nova senha" name="newPasswordConfirm" type="password" autoComplete="new-password" required maxLength={32} error={e.newPasswordConfirm} />
      <SubmitButton>Trocar senha</SubmitButton>
    </form>
  );
}

export function ChangeEmailForm({ current }: { current: string | null }) {
  const [state, action] = useActionState(changeEmailAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <Field
        label={current ? "Novo e-mail" : "E-mail"}
        name="email"
        type="email"
        autoComplete="email"
        required
        maxLength={254}
        defaultValue={state.values?.email}
        error={state.fieldErrors?.email}
      />
      <SubmitButton variant="arcane">{current ? "Trocar e-mail" : "Cadastrar e-mail"}</SubmitButton>
    </form>
  );
}

export function ResendConfirmationButton() {
  const [state, action] = useActionState(resendConfirmationAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-2">
      <FormMessage state={state} />
      {state.status !== "success" ? (
        <button type="submit" className="self-start text-sm text-arcane underline-offset-2 hover:underline">
          Reenviar link de confirmação
        </button>
      ) : null}
    </form>
  );
}

export function RevokeOthersButton() {
  const [state, action] = useActionState(revokeOtherSessionsAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-2">
      <FormMessage state={state} />
      <button type="submit" className="self-start rounded-sm border border-bronze px-3 py-1.5 text-sm text-gold hover:border-gold">
        Encerrar outras sessões
      </button>
    </form>
  );
}
