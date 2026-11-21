"use client";

import { useActionState, useRef, type ReactNode } from "react";
import { Field } from "@/components/forms/Field";
import { FormMessage } from "@/components/forms/FormMessage";
import { SubmitButton } from "@/components/forms/SubmitButton";
import { ItemPicker } from "@/features/admin/ItemPicker";
import { initialFormState, type FormState } from "@/features/auth/types";
import {
  banAction,
  coinsAction,
  rankingResetAction,
  rankingRestoreAction,
  rowDeleteAction,
  rowInsertAction,
  rowUpdateAction,
  serverAction,
  sourceConfigAction,
  sourceRemoveAction,
  unbanAction,
  vipAddAction,
  vipExtendAction,
  vipRemoveAction,
} from "./gameActions";
import { Select } from "./inputs";
import type { AccountVip, CatalogItem, GameColumnView, GameSourceBinding } from "./types";

type Action = (state: FormState, form: FormData) => Promise<FormState>;

const tones = {
  danger: "border-data-bad/70 text-data-bad hover:bg-data-bad/10",
  gold: "border-bronze text-gold hover:border-gold hover:bg-gold/10",
  calm: "border-steel text-silver/80 hover:border-arcane hover:text-arcane",
} as const;

/** Botão que abre uma janela de confirmação com o formulário dentro. Fecha com Esc ou no X. */
export function ConfirmDialog({ label, title, tone = "gold", children, small }: { label: ReactNode; title: string; tone?: keyof typeof tones; children: ReactNode; small?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null);
  return (
    <>
      <button type="button" onClick={() => ref.current?.showModal()} className={`rounded-sm border ${small ? "px-2 py-1 text-xs" : "px-3 py-1.5 text-sm"} transition-colors ${tones[tone]}`}>
        {label}
      </button>
      <dialog ref={ref} aria-label={title} className="m-auto w-[min(92vw,38rem)] rounded-md border border-bronze/60 bg-abyss p-0 text-silver shadow-2xl backdrop:bg-night/80 backdrop:backdrop-blur-sm">
        <div className="flex items-center justify-between border-b border-steel/70 px-4 py-3">
          <h3 className="font-display text-base text-gold-bright">{title}</h3>
          <button type="button" onClick={() => ref.current?.close()} className="text-silver/60 hover:text-silver" aria-label="Fechar">
            ✕
          </button>
        </div>
        <div className="max-h-[75vh] overflow-y-auto p-4">{children}</div>
      </dialog>
    </>
  );
}

function useForm(action: Action) {
  const [state, run] = useActionState(action, initialFormState);
  return { state, run, e: state.fieldErrors ?? {}, v: (k: string) => state.values?.[k] };
}

function ReasonField({ error, value, label = "Motivo" }: { error?: string; value?: string; label?: string }) {
  return <Field label={label} name="reason" required minLength={3} maxLength={300} defaultValue={value} error={error} hint="Fica registrado na auditoria com o seu nome." />;
}

function PasswordField({ error }: { error?: string }) {
  return <Field label="Sua senha do painel" name="password" type="password" autoComplete="current-password" required maxLength={128} error={error} hint="Confirmação de segurança para ações que não dá para desfazer." />;
}

function ConfirmWord({ word, error }: { word: string; error?: string }) {
  return <Field label={`Digite ${word} para confirmar`} name="confirm" required autoComplete="off" spellCheck={false} error={error} />;
}

function Warning({ children }: { children: ReactNode }) {
  return <p className="rounded-sm border border-data-name/40 bg-data-name/5 px-3 py-2 text-sm text-data-name">{children}</p>;
}

// ---------- contas ----------

export function BanForm({ login }: { login: string }) {
  const { state, run, e, v } = useForm(banAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <Warning>
        A conta <strong>{login}</strong> não vai conseguir entrar no jogo nem no site. Quem estiver online continua até sair.
      </Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <Field label="Dias de banimento" name="days" type="number" min={1} max={3650} defaultValue={v("days")} error={e.days} hint="Vazio = sem prazo. Com prazo, a conta é liberada sozinha." />
      <PasswordField error={e.password} />
      <SubmitButton>Banir conta</SubmitButton>
    </form>
  );
}

export function UnbanForm({ login }: { login: string }) {
  const { state, run, e, v } = useForm(unbanAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <SubmitButton variant="arcane">Desbanir conta</SubmitButton>
    </form>
  );
}

export function CoinsForm({ login, current }: { login: string; current: number | null }) {
  const { state, run, e, v } = useForm(coinsAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <p className="text-sm text-silver/70">
        Saldo atual: <strong className="tabular text-data-name">{current == null ? "sem registro" : current.toLocaleString("pt-BR")}</strong>
      </p>
      <FormMessage state={state} />
      <div className="grid gap-3 sm:grid-cols-2">
        <Select label="O que fazer" name="mode" options={{ add: "Adicionar", remove: "Remover", set: "Definir saldo exato" }} defaultValue={v("mode") ?? "add"} error={e.mode} />
        <Field label="Quantidade" name="amount" type="number" min={0} required defaultValue={v("amount")} error={e.amount} />
      </div>
      <ReasonField error={e.reason} value={v("reason")} label="Motivo (ex.: doação PIX 20/11)" />
      <SubmitButton>Salvar coins</SubmitButton>
    </form>
  );
}

export function VipAddForm({ login, plans, characters }: { login: string; plans: Record<string, string>; characters: string[] }) {
  const { state, run, e, v } = useForm(vipAddAction);
  const planOptions = Object.keys(plans).length > 0 ? plans : null;
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <FormMessage state={state} />
      <div className="grid gap-3 sm:grid-cols-2">
        {planOptions ? (
          <Select label="Plano" name="plan" options={planOptions} defaultValue={v("plan")} error={e.plan} />
        ) : (
          <Field label="Plano (número do IdVip)" name="plan" type="number" min={0} max={1000} required defaultValue={v("plan")} error={e.plan} hint="Os nomes dos planos podem ser cadastrados em GameAdmin:VipPlans." />
        )}
        <Field label="Dias" name="days" type="number" min={1} required defaultValue={v("days") ?? "30"} error={e.days} />
      </div>
      <Select
        label="Personagem (opcional)"
        name="character"
        options={{ "": "Conta inteira", ...Object.fromEntries(characters.map((c) => [c, c])) }}
        defaultValue={v("character") ?? ""}
        error={e.character}
      />
      <ReasonField error={e.reason} value={v("reason")} />
      <SubmitButton>Adicionar VIP</SubmitButton>
    </form>
  );
}

const rowJson = (vip: AccountVip) => JSON.stringify({ playerName: vip.playerName, plan: vip.plan, startLocal: vip.startLocal, endLocal: vip.endLocal });

export function VipExtendForm({ login, vip }: { login: string; vip: AccountVip }) {
  const { state, run, e, v } = useForm(vipExtendAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <input type="hidden" name="row" value={rowJson(vip)} />
      <FormMessage state={state} />
      <Field label="Dias a mais" name="days" type="number" required defaultValue={v("days") ?? "30"} error={e.days} hint="VIP vencido recomeça a contar de agora. Número negativo diminui." />
      <ReasonField error={e.reason} value={v("reason")} />
      <SubmitButton>Estender VIP</SubmitButton>
    </form>
  );
}

export function VipRemoveForm({ login, vip }: { login: string; vip: AccountVip }) {
  const { state, run, e, v } = useForm(vipRemoveAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="login" value={login} />
      <input type="hidden" name="row" value={rowJson(vip)} />
      <Warning>O registro de VIP é apagado do jogo. Os dados ficam na auditoria.</Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <PasswordField error={e.password} />
      <SubmitButton>Remover VIP</SubmitButton>
    </form>
  );
}

// ---------- servidor e rankings ----------

export function ServerActionForm({ target, action, word, warning }: { target: string; action: "start" | "stop" | "restart"; word: string; warning: string }) {
  const { state, run, e, v } = useForm(serverAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="target" value={target} />
      <input type="hidden" name="action" value={action} />
      <Warning>{warning}</Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <ConfirmWord word={word} error={e.confirm} />
      <PasswordField error={e.password} />
      <SubmitButton>Confirmar</SubmitButton>
      <p className="text-xs text-silver/50">Desligar espera o programa fechar sozinho e só força depois do prazo. Pode levar até alguns minutos.</p>
    </form>
  );
}

export function RankingResetForm({ kind, label, word }: { kind: string; label: string; word: string }) {
  const { state, run, e, v } = useForm(rankingResetAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="kind" value={kind} />
      <Warning>
        {label}: todos os valores vão para zero. Antes, o painel guarda uma cópia; dá para desfazer na lista de cópias abaixo.
      </Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <ConfirmWord word={word} error={e.confirm} />
      <PasswordField error={e.password} />
      <SubmitButton>Zerar ranking</SubmitButton>
    </form>
  );
}

export function RankingRestoreForm({ id }: { id: number }) {
  const { state, run, e, v } = useForm(rankingRestoreAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="id" value={id} />
      <Warning>Os valores da cópia voltam para o jogo. Pontos feitos depois do reset são substituídos.</Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <PasswordField error={e.password} />
      <SubmitButton>Desfazer reset</SubmitButton>
    </form>
  );
}

// ---------- tabelas do jogo ----------

export function ColumnInput({ col, value, error, required }: { col: GameColumnView; value?: string | null; error?: string; required?: boolean }) {
  const name = `v:${col.name}`;
  const label = `${col.name}${col.nullable ? "" : " *"}`;
  const hint = [col.type + (col.maxChars > 0 && col.maxChars < 100000 && ["char", "varchar", "nchar", "nvarchar"].includes(col.type) ? `(${col.maxChars})` : ""), col.hasDefault ? "vazio = padrão da tabela" : null, col.nullable ? "vazio = NULL" : null]
    .filter(Boolean)
    .join(" · ");
  if (col.type === "bit") {
    return <Select label={label} name={name} options={{ ...(col.nullable || col.hasDefault ? { "": "—" } : {}), "1": "Sim (1)", "0": "Não (0)" }} defaultValue={value ?? ""} error={error} />;
  }
  const isNumber = ["tinyint", "smallint", "int", "bigint"].includes(col.type);
  const isDate = ["datetime", "smalldatetime", "datetime2", "date"].includes(col.type);
  return (
    <Field
      label={label}
      name={name}
      type={isNumber ? "number" : "text"}
      inputMode={isNumber ? "numeric" : ["decimal", "numeric", "money", "smallmoney", "float", "real"].includes(col.type) ? "decimal" : undefined}
      placeholder={isDate ? "2026-11-20T14:30" : undefined}
      maxLength={!isNumber && col.maxChars > 0 && col.maxChars < 100000 ? col.maxChars : undefined}
      defaultValue={value ?? ""}
      required={required}
      error={error}
      hint={hint}
    />
  );
}

function KeyInputs({ columns, row }: { columns: GameColumnView[]; row: Record<string, string | null> }) {
  return (
    <>
      {columns
        .filter((c) => c.isKey)
        .map((c) => (row[c.name] == null ? <input key={c.name} type="hidden" name={`n:${c.name}`} value="" /> : <input key={c.name} type="hidden" name={`k:${c.name}`} value={row[c.name] ?? ""} />))}
    </>
  );
}

export function RowEditForm({ fn, columns, row }: { fn: string; columns: GameColumnView[]; row: Record<string, string | null> }) {
  const { state, run, e, v } = useForm(rowUpdateAction);
  const keyText = columns.filter((c) => c.isKey).map((c) => `${c.name} = ${row[c.name] ?? "NULL"}`).join(", ");
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="fn" value={fn} />
      <KeyInputs columns={columns} row={row} />
      <p className="text-xs text-silver/60">Linha: {keyText}</p>
      <FormMessage state={state} />
      <div className="grid gap-3 sm:grid-cols-2">
        {columns
          .filter((c) => c.isEditable)
          .map((c) => (
            <ColumnInput key={c.name} col={c} value={v(`v:${c.name}`) ?? row[c.name]} error={e[c.name]} />
          ))}
      </div>
      {e.values ? <p className="text-xs text-data-bad">{e.values}</p> : null}
      <ReasonField error={e.reason} value={v("reason")} />
      <SubmitButton>Salvar linha</SubmitButton>
    </form>
  );
}

export function RowDeleteForm({ fn, columns, row }: { fn: string; columns: GameColumnView[]; row: Record<string, string | null> }) {
  const { state, run, e, v } = useForm(rowDeleteAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="fn" value={fn} />
      <KeyInputs columns={columns} row={row} />
      <Warning>Esta linha será apagada da tabela do jogo. Os valores ficam guardados na auditoria.</Warning>
      <FormMessage state={state} />
      <ReasonField error={e.reason} value={v("reason")} />
      <PasswordField error={e.password} />
      <SubmitButton>Apagar linha</SubmitButton>
    </form>
  );
}

export function RowInsertForm({
  fn,
  columns,
  submitLabel,
  accountHint,
  items,
}: {
  fn: string;
  columns: GameColumnView[];
  submitLabel: string;
  accountHint?: string;
  /** Colunas de código de item, que ganham "Escolher pela foto". */
  items?: { columns: string[]; catalog: CatalogItem[]; lowercase: boolean };
}) {
  const { state, run, e, v } = useForm(rowInsertAction);
  const fields = columns.filter((c) => c.insertable);
  const account = fields.find((c) => c.isAccount);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="fn" value={fn} />
      <FormMessage state={state} />
      {account ? (
        <div className="rounded-sm border border-arcane/40 bg-arcane/5 p-3">
          <ColumnInput col={account} value={v(`v:${account.name}`)} error={e[account.name]} required />
          {accountHint ? <p className="mt-1 text-xs text-silver/60">{accountHint}</p> : null}
        </div>
      ) : null}
      <div className="grid gap-3 sm:grid-cols-2">
        {fields
          .filter((c) => !c.isAccount)
          .map((c) =>
            items?.columns.includes(c.name) ? (
              <ItemPicker
                key={c.name}
                name={`v:${c.name}`}
                label={`${c.name}${c.nullable ? "" : " *"}`}
                defaultValue={v(`v:${c.name}`)}
                error={e[c.name]}
                catalog={items.catalog}
                lowercase={items.lowercase}
                maxLength={c.maxChars > 0 && c.maxChars < 100000 ? c.maxChars : undefined}
              />
            ) : (
              <ColumnInput key={c.name} col={c} value={v(`v:${c.name}`)} error={e[c.name]} />
            ),
          )}
      </div>
      {e.values ? <p className="text-xs text-data-bad">{e.values}</p> : null}
      <ReasonField error={e.reason} value={v("reason")} />
      <SubmitButton>{submitLabel}</SubmitButton>
    </form>
  );
}

export function SourceConfigForm({
  fn,
  mode,
  database,
  table,
  columns,
  binding,
}: {
  fn: string;
  mode: "edit" | "insert" | "readonly";
  database: string;
  table: string;
  columns: GameColumnView[];
  binding: GameSourceBinding | null;
}) {
  const { state, run, e } = useForm(sourceConfigAction);
  const same = binding && binding.database === database && binding.table === table ? binding : null;
  const isText = (c: GameColumnView) => ["char", "varchar", "nchar", "nvarchar"].includes(c.type);
  const isInt = (c: GameColumnView) => ["tinyint", "smallint", "int", "bigint"].includes(c.type);
  return (
    <form action={run} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="fn" value={fn} />
      <input type="hidden" name="database" value={database} />
      <input type="hidden" name="table" value={table} />
      <FormMessage state={state} />
      {e.table ? <p className="text-sm text-data-bad">{e.table}</p> : null}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-sm">
          <thead>
            <tr className="text-left text-xs text-silver/60">
              <th scope="col" className="py-1 font-normal">Coluna</th>
              <th scope="col" className="py-1 font-normal">Tipo</th>
              <th scope="col" className="py-1 text-center font-normal">Chave</th>
              {mode === "edit" ? <th scope="col" className="py-1 text-center font-normal">Editável</th> : null}
              <th scope="col" className="py-1 text-center font-normal">Busca</th>
              {mode === "insert" ? <th scope="col" className="py-1 text-center font-normal">Conta</th> : null}
            </tr>
          </thead>
          <tbody>
            {columns.map((c) => (
              <tr key={c.name} className="border-t border-steel/60">
                <td className="py-1.5 font-mono text-xs">
                  {c.name}
                  {c.identity ? <span className="ml-1 text-silver/50">(automático)</span> : null}
                </td>
                <td className="py-1.5 text-xs text-silver/60">
                  {c.type}
                  {c.nullable ? "" : ", obrigatório"}
                  {c.supported ? "" : ", não suportado"}
                </td>
                <td className="py-1.5 text-center">
                  <input type="checkbox" name="keyColumns" value={c.name} disabled={!c.supported || ["float", "real"].includes(c.type)} defaultChecked={same?.keyColumns.includes(c.name)} aria-label={`${c.name} é chave`} />
                </td>
                {mode === "edit" ? (
                  <td className="py-1.5 text-center">
                    <input type="checkbox" name="editableColumns" value={c.name} disabled={!c.supported || c.identity || c.computed} defaultChecked={same?.editableColumns.includes(c.name)} aria-label={`${c.name} editável`} />
                  </td>
                ) : null}
                <td className="py-1.5 text-center">
                  <input type="checkbox" name="searchColumns" value={c.name} disabled={!(isText(c) || isInt(c))} defaultChecked={same?.searchColumns.includes(c.name)} aria-label={`${c.name} na busca`} />
                </td>
                {mode === "insert" ? (
                  <td className="py-1.5 text-center">
                    <input type="radio" name="accountColumn" value={c.name} disabled={!isText(c)} defaultChecked={same?.accountColumn === c.name} aria-label={`${c.name} recebe a conta`} />
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {["keyColumns", "editableColumns", "searchColumns", "accountColumn"].map((k) => (e[k] ? <p key={k} className="text-xs text-data-bad">{e[k]}</p> : null))}
      <div className="flex flex-wrap gap-4 text-sm">
        {mode === "edit" ? (
          <>
            <label className="flex items-center gap-2">
              <input type="checkbox" name="allowInsert" defaultChecked={same?.allowInsert} /> Permitir incluir linhas
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" name="allowDelete" defaultChecked={same?.allowDelete} /> Permitir apagar linhas
            </label>
          </>
        ) : null}
        <label className="flex items-center gap-2">
          <input type="checkbox" name="newestFirst" defaultChecked={same?.newestFirst ?? mode !== "edit"} /> Mostrar do maior para o menor (mais novos primeiro)
        </label>
      </div>
      <PasswordField error={e.password} />
      <SubmitButton>Salvar configuração</SubmitButton>
    </form>
  );
}

export function SourceRemoveForm({ fn }: { fn: string }) {
  const { state, run, e } = useForm(sourceRemoveAction);
  return (
    <form action={run} className="flex flex-col gap-3" noValidate>
      <input type="hidden" name="fn" value={fn} />
      <Warning>A função deixa de funcionar até ser configurada de novo. Nada é apagado do jogo.</Warning>
      <FormMessage state={state} />
      <PasswordField error={e.password} />
      <SubmitButton>Desligar função</SubmitButton>
    </form>
  );
}
