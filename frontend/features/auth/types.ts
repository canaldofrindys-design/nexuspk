export type FormState = {
  status: "idle" | "error" | "success";
  message?: string;
  fieldErrors?: Record<string, string>;
  /** Valores para repreencher o formulário depois de um erro (nunca a senha). */
  values?: Record<string, string>;
};

export const initialFormState: FormState = { status: "idle" };

export type AuthOptions = {
  registrationOpen: boolean;
  recoveryAvailable: boolean;
  captchaEnabled: boolean;
  loginMinLength: number;
  loginMaxLength: number;
  passwordMinLength: number;
  passwordMaxLength: number;
};
