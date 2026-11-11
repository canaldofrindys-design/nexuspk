import type { ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";

const base = "rounded-sm border bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane focus-visible:ring-1 focus-visible:ring-arcane";

function Wrap({ id, label, error, hint, children }: { id: string; label: string; error?: string; hint?: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm text-silver">
        {label}
      </label>
      {children}
      {hint && !error ? (
        <p id={`${id}-hint`} className="text-xs text-silver/60">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${id}-err`} className="text-xs text-data-bad">
          {error}
        </p>
      ) : null}
    </div>
  );
}

const describedBy = (id: string, error?: string, hint?: string) =>
  [error ? `${id}-err` : null, hint ? `${id}-hint` : null].filter(Boolean).join(" ") || undefined;

export function TextArea({ label, name, error, hint, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement> & { label: string; name: string; error?: string; hint?: string }) {
  const id = `f-${name}`;
  return (
    <Wrap id={id} label={label} error={error} hint={hint}>
      <textarea
        id={id}
        name={name}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, error, hint)}
        className={`${base} min-h-24 ${error ? "border-data-bad" : "border-bronze/70"}`}
        {...rest}
      />
    </Wrap>
  );
}

export function Select({
  label,
  name,
  error,
  options,
  id: idProp,
  ...rest
}: SelectHTMLAttributes<HTMLSelectElement> & { label: string; name: string; error?: string; options: Record<string, string> }) {
  const id = idProp ?? `f-${name}`;
  return (
    <Wrap id={id} label={label} error={error}>
      <select key={String(rest.defaultValue ?? "")} id={id} name={name} aria-invalid={error ? true : undefined} aria-describedby={describedBy(id, error)} className={`${base} ${error ? "border-data-bad" : "border-bronze/70"}`} {...rest}>
        {Object.entries(options).map(([value, text]) => (
          <option key={value} value={value}>
            {text}
          </option>
        ))}
      </select>
    </Wrap>
  );
}
