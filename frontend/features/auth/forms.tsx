"use client";

import Link from "next/link";
import { useActionState } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { Turnstile } from "@/components/forms/Turnstile";
import { confirmEmailAction, loginAction, recoverAction, registerAction, resetPasswordAction } from "./actions";
import { initialFormState, type AuthOptions } from "./types";

export function RegisterForm({ options }: { options: AuthOptions }) {
  const [state, action] = useActionState(registerAction, initialFormState);
  const e = state.fieldErrors ?? {};
  if (state.status === "success") {
    return (
      <div className="flex flex-col gap-3">
        <FormMessage state={state} />
        <Link href="/login" className="text-sm text-arcane underline-offset-2 hover:underline">
          Entrar no painel
        </Link>
      </div>
    );
  }
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <Field
        label="Login"
        name="login"
        autoComplete="username"
        required
        minLength={options.loginMinLength}
        maxLength={options.loginMaxLength}
        defaultValue={state.values?.login}
        error={e.login}
        hint={`De ${options.loginMinLength} a ${options.loginMaxLength} caracteres: letras, números, _ ou -. É o login do jogo.`}
      />
      <Field
        label="E-mail"
        name="email"
        type="email"
        autoComplete="email"
        required
        maxLength={254}
        defaultValue={state.values?.email}
        error={e.email}
        hint="Usado só para recuperar a conta."
      />
      <Field
        label="Senha"
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={options.passwordMinLength}
        maxLength={options.passwordMaxLength}
        error={e.password}
        hint={`De ${options.passwordMinLength} a ${options.passwordMaxLength} letras e números.`}
      />
      <Field
        label="Repita a senha"
        name="passwordConfirm"
        type="password"
        autoComplete="new-password"
        required
        maxLength={options.passwordMaxLength}
        error={e.passwordConfirm}
      />
      <Turnstile />
      <SubmitButton>Criar conta</SubmitButton>
    </form>
  );
}

export function LoginForm({ next }: { next: string }) {
  const [state, action] = useActionState(loginAction, initialFormState);
  return (
    <form action={action} className="flex flex-col gap-3">
      <FormMessage state={state} />
      <input type="hidden" name="next" value={next} />
      <Field label="Login" name="login" autoComplete="username" required maxLength={32} defaultValue={state.values?.login} />
      <Field label="Senha" name="password" type="password" autoComplete="current-password" required maxLength={32} />
      <Turnstile />
      <SubmitButton variant="arcane">Entrar</SubmitButton>
    </form>
  );
}

export function RecoverForm() {
  const [state, action] = useActionState(recoverAction, initialFormState);
  if (state.status === "success") return <FormMessage state={state} />;
  return (
    <form action={action} className="flex flex-col gap-3">
      <FormMessage state={state} />
      <Field label="Login" name="login" autoComplete="username" required maxLength={32} defaultValue={state.values?.login} />
      <Field label="E-mail da conta" name="email" type="email" autoComplete="email" required maxLength={254} defaultValue={state.values?.email} />
      <Turnstile />
      <SubmitButton>Enviar link</SubmitButton>
    </form>
  );
}

export function ResetPasswordForm({ token, options }: { token: string; options: AuthOptions }) {
  const [state, action] = useActionState(resetPasswordAction, initialFormState);
  const e = state.fieldErrors ?? {};
  if (state.status === "success") {
    return (
      <div className="flex flex-col gap-3">
        <FormMessage state={state} />
        <Link href="/login" className="text-sm text-arcane underline-offset-2 hover:underline">
          Entrar no painel
        </Link>
      </div>
    );
  }
  return (
    <form action={action} className="flex flex-col gap-3" noValidate>
      <FormMessage state={state} />
      <input type="hidden" name="token" value={token} />
      <Field
        label="Nova senha"
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={options.passwordMinLength}
        maxLength={options.passwordMaxLength}
        error={e.password}
        hint={`De ${options.passwordMinLength} a ${options.passwordMaxLength} letras e números.`}
      />
      <Field label="Repita a nova senha" name="passwordConfirm" type="password" autoComplete="new-password" required error={e.passwordConfirm} />
      <SubmitButton>Salvar nova senha</SubmitButton>
    </form>
  );
}

export function ConfirmEmailForm({ token }: { token: string }) {
  const [state, action] = useActionState(confirmEmailAction, initialFormState);
  if (state.status === "success") return <FormMessage state={state} />;
  return (
    <form action={action} className="flex flex-col gap-3">
      <FormMessage state={state} />
      <input type="hidden" name="token" value={token} />
      <p className="text-sm text-silver/80">Clique para confirmar o e-mail desta conta.</p>
      <SubmitButton>Confirmar e-mail</SubmitButton>
    </form>
  );
}
