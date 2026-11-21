"use client";

import { LoaderCircle, Plus, Trash2 } from "lucide-react";
import { startTransition, useActionState, useRef, useState, type FormEvent } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { ItemPicker } from "@/features/admin/ItemPicker";
import { initialFormState } from "@/features/auth/types";
import { donateBoxAction, type DonateBoxItemValue } from "./gameActions";
import { Select } from "./inputs";
import type { CatalogItem } from "./types";

export type DonateCharacter = { name: string; className: string; level: number | null };

const inputClass = "w-full rounded-sm border bg-night/80 px-3 py-2 text-sm text-silver outline-none focus-visible:border-arcane";

/**
 * Monta a Box Donate: personagem, itens (código, quantidade, aging e mix) e motivo.
 * O formulário não usa action direto para não apagar as linhas quando a API recusa algum campo.
 */
export function DonateBoxForm({
  login,
  characters,
  catalog,
  maxItems,
  maxAging,
}: {
  login: string;
  characters: DonateCharacter[];
  catalog: CatalogItem[];
  maxItems: number;
  maxAging: number;
}) {
  const [state, run, pending] = useActionState(donateBoxAction, initialFormState);
  const nextId = useRef(1);
  const [rows, setRows] = useState<number[]>([0]);
  const [formKey, setFormKey] = useState(0);
  const e = state.fieldErrors ?? {};
  const v = (k: string) => state.values?.[k];
  const [seen, setSeen] = useState(state);

  // Depois de um envio certo, começa uma caixa nova (mesmo personagem).
  if (state !== seen) {
    setSeen(state);
    if (state.status === "success") {
      setRows([0]);
      setFormKey((k) => k + 1);
    }
  }

  function submit(ev: FormEvent<HTMLFormElement>) {
    ev.preventDefault();
    const fd = new FormData(ev.currentTarget);
    const num = (name: string) => {
      const raw = String(fd.get(name) ?? "").trim();
      return raw === "" ? null : Number(raw);
    };
    const items: DonateBoxItemValue[] = rows.map((id) => ({
      code: String(fd.get(`code-${id}`) ?? "").trim().toUpperCase(),
      quantity: num(`quantity-${id}`),
      aging: num(`aging-${id}`),
      mix: fd.get(`mix-${id}`) === "on",
    }));
    const send = new FormData();
    send.set("login", login);
    send.set("character", String(fd.get("character") ?? ""));
    send.set("reason", String(fd.get("reason") ?? ""));
    send.set("items", JSON.stringify(items));
    startTransition(() => run(send));
  }

  const characterOptions = Object.fromEntries(
    characters.map((c) => [c.name, `${c.name} (${c.className}${c.level != null ? `, nível ${c.level}` : ""})`]),
  );

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      <FormMessage state={state} />
      <Select
        label="Personagem que vai receber"
        name="character"
        options={{ "": "Escolha…", ...characterOptions }}
        defaultValue={v("character") ?? (characters.length === 1 ? characters[0].name : "")}
        error={e.character ?? e.login}
      />

      <fieldset key={formKey} className="flex flex-col gap-3">
        <legend className="mb-2 text-sm text-silver">Itens dentro da caixa</legend>
        {rows.map((id, index) => (
          <div key={id} className="grid gap-3 rounded-sm border border-steel/60 bg-night/40 p-3 sm:grid-cols-[1fr_auto]">
            <div className="grid gap-3 sm:grid-cols-2">
              <ItemPicker name={`code-${id}`} label={`Item ${index + 1}`} catalog={catalog} lowercase={false} maxLength={7} error={e[`items.${index}.code`]} />
              <div className="flex flex-col gap-1">
                <label htmlFor={`f-quantity-${id}`} className="text-sm text-silver">Quantidade</label>
                <input id={`f-quantity-${id}`} name={`quantity-${id}`} type="number" min={1} max={maxItems} defaultValue={1} className={`${inputClass} ${e[`items.${index}.quantity`] ? "border-data-bad" : "border-bronze/70"}`} />
                {e[`items.${index}.quantity`] ? <p className="text-xs text-data-bad">{e[`items.${index}.quantity`]}</p> : null}
              </div>
              <div className="flex flex-col gap-1">
                <label htmlFor={`f-aging-${id}`} className="text-sm text-silver">Aging (0 a {maxAging})</label>
                <input id={`f-aging-${id}`} name={`aging-${id}`} type="number" min={0} max={maxAging} defaultValue={0} className={`${inputClass} ${e[`items.${index}.aging`] ? "border-data-bad" : "border-bronze/70"}`} />
                {e[`items.${index}.aging`] ? <p className="text-xs text-data-bad">{e[`items.${index}.aging`]}</p> : null}
              </div>
              <label className="flex items-center gap-2 self-end pb-2 text-sm text-silver">
                <input type="checkbox" name={`mix-${id}`} className="size-4 accent-[var(--color-gold,#d4a94a)]" />
                Com mix
              </label>
            </div>
            {rows.length > 1 ? (
              <button
                type="button"
                onClick={() => setRows((r) => r.filter((x) => x !== id))}
                className="inline-flex items-center gap-1 self-start rounded-sm border border-steel px-2 py-1 text-xs text-silver/70 hover:border-data-bad hover:text-data-bad"
                aria-label={`Tirar o item ${index + 1}`}
              >
                <Trash2 className="size-3.5" aria-hidden /> Tirar
              </button>
            ) : null}
          </div>
        ))}
        {e.items ? <p className="text-xs text-data-bad">{e.items}</p> : null}
        {rows.length < maxItems ? (
          <button
            type="button"
            onClick={() => {
              const id = nextId.current++;
              setRows((r) => [...r, id]);
            }}
            className="inline-flex w-fit items-center gap-1.5 rounded-sm border border-arcane/70 px-3 py-1.5 text-sm text-arcane hover:bg-arcane/10"
          >
            <Plus className="size-4" aria-hidden /> Outro item
          </button>
        ) : null}
      </fieldset>

      <Field key={`reason-${formKey}`} label="Motivo (ex.: doação PIX 20/11)" name="reason" required minLength={3} maxLength={300} defaultValue={v("reason")} error={e.reason} hint="Fica registrado na auditoria com o seu nome." />
      <button
        type="submit"
        disabled={pending}
        className="btn-glow nx-btn inline-flex w-full items-center justify-center gap-2 rounded-sm border px-4 py-2 text-sm font-semibold disabled:opacity-70"
      >
        {pending ? <LoaderCircle className="size-4 animate-spin" aria-hidden /> : null}
        Enviar Box Donate
      </button>
    </form>
  );
}
