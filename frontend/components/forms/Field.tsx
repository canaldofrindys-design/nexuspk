import type { InputHTMLAttributes } from "react";

type Props = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
  name: string;
  error?: string;
  hint?: string;
};

export function Field({ label, name, error, hint, id, ...input }: Props) {
  const fieldId = id ?? `f-${name}`;
  const describedBy = [error ? `${fieldId}-err` : null, hint ? `${fieldId}-hint` : null].filter(Boolean).join(" ") || undefined;
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={fieldId} className="text-sm text-silver">
        {label}
      </label>
      <input
        id={fieldId}
        name={name}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={`rounded-sm border bg-night/80 px-3 py-2 text-sm text-silver outline-none placeholder:text-silver/40 focus-visible:border-arcane focus-visible:ring-1 focus-visible:ring-arcane ${
          error ? "border-data-bad" : "border-bronze/70"
        }`}
        {...input}
      />
      {hint && !error ? (
        <p id={`${fieldId}-hint`} className="text-xs text-silver/60">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${fieldId}-err`} className="text-xs text-data-bad">
          {error}
        </p>
      ) : null}
    </div>
  );
}
