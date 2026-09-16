"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { HistoryIcon, PlusIcon, RefreshIcon } from "@/components/icons";
import { SidePanel } from "@/components/side-panel";
import type { AccountStatus, AccountType, BankAccount, LedgerEntry } from "@/lib/contracts";
import { cashIn, createAccount, listAccounts, listLedger, updateAccount } from "./accounts-api";

const money = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const date = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" });
type Panel = { kind: "create" } | { kind: "cash" | "settings" | "ledger"; account: BankAccount } | null;

export function AccountsConsole() {
  const [accounts, setAccounts] = useState<BankAccount[]>([]);
  const [search, setSearch] = useState("");
  const [panel, setPanel] = useState<Panel>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setAccounts((await listAccounts(search)).items);
      setError(null);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Falha ao carregar contas.");
    } finally {
      setLoading(false);
    }
  }, [search]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const metrics = useMemo(() => ({
    balance: accounts.reduce((sum, item) => sum + item.balance, 0),
    active: accounts.filter((item) => item.status === "Active").length,
    blocked: accounts.filter((item) => item.status === "Blocked").length,
  }), [accounts]);

  function saved(message: string) {
    setPanel(null);
    setNotice(message);
    void load();
  }

  return (
    <main className="bank-page">
      <header className="page-heading bank-heading">
        <div><span className="eyebrow">Core banking · contas</span><h1>Contas e saldos</h1><p>Gerencie clientes, limites, saldos e o razão auditável.</p></div>
        <button className="primary-button compact-button" onClick={() => setPanel({ kind: "create" })}><PlusIcon /> Abrir conta</button>
      </header>

      <section className="bank-stats">
        <article className="purple-stat"><span>Saldo sob gestão</span><strong>{money.format(metrics.balance)}</strong><small>somatório das contas exibidas</small></article>
        <article><span>Contas ativas</span><strong>{metrics.active}</strong><small>aptas a movimentar</small></article>
        <article><span>Contas bloqueadas</span><strong>{metrics.blocked}</strong><small>proteção operacional</small></article>
        <article><span>Clientes encontrados</span><strong>{accounts.length}</strong><small>consulta sem cache</small></article>
      </section>

      {notice && <div className="bank-message success" role="status">{notice}<button onClick={() => setNotice(null)}>×</button></div>}
      {error && <div className="bank-message error" role="alert">{error}</div>}

      <section className="data-card">
        <div className="bank-toolbar">
          <form onSubmit={(event) => { event.preventDefault(); void load(); }}><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por titular, conta ou chave Pix"/><button>Buscar</button></form>
          <button className="icon-button" onClick={() => void load()} aria-label="Atualizar"><RefreshIcon /></button>
        </div>
        <div className="table-scroll">
          <table className="bank-table">
            <thead><tr><th>Cliente</th><th>Conta</th><th>Situação</th><th>Saldo</th><th>Limites</th><th>Ações</th></tr></thead>
            <tbody>
              {accounts.map((account) => <tr key={account.id}>
                <td><strong>{account.holderName}</strong><small>{account.document} · {account.pixKey}</small></td>
                <td><strong>{account.number}</strong><small>{account.type === "Checking" ? "Conta corrente" : "Conta poupança"}</small></td>
                <td><span className={`status-pill ${account.status.toLowerCase()}`}>{statusLabel(account.status)}</span></td>
                <td className="money-cell">{money.format(account.balance)}</td>
                <td><strong>{money.format(account.dailyTransferLimit)}</strong><small>Noturno: {money.format(account.nightlyTransferLimit)}</small></td>
                <td><div className="row-actions"><button onClick={() => setPanel({ kind: "cash", account })}>Depositar</button><button onClick={() => setPanel({ kind: "settings", account })}>Limites</button><button onClick={() => setPanel({ kind: "ledger", account })}><HistoryIcon /> Extrato</button></div></td>
              </tr>)}
            </tbody>
          </table>
        </div>
        {!loading && accounts.length === 0 && <div className="empty-bank">Nenhuma conta encontrada.</div>}
        {loading && <div className="empty-bank">Carregando contas…</div>}
      </section>

      {panel?.kind === "create" && <SidePanel eyebrow="Onboarding" title="Abrir nova conta" onClose={() => setPanel(null)}><CreateAccountForm onSaved={() => saved("Conta criada com saldo inicial auditado.")} /></SidePanel>}
      {panel?.kind === "cash" && <SidePanel eyebrow="Movimentação simulada" title="Adicionar saldo" onClose={() => setPanel(null)}><CashInForm account={panel.account} onSaved={() => saved("Aporte registrado no razão da conta.")} /></SidePanel>}
      {panel?.kind === "settings" && <SidePanel eyebrow="Proteção financeira" title="Limites e situação" onClose={() => setPanel(null)}><SettingsForm account={panel.account} onSaved={() => saved("Limites e situação atualizados.")} /></SidePanel>}
      {panel?.kind === "ledger" && <SidePanel eyebrow="Auditoria" title={`Extrato · ${panel.account.number}`} onClose={() => setPanel(null)} wide><Ledger account={panel.account} /></SidePanel>}
    </main>
  );
}

function CreateAccountForm({ onSaved }: { onSaved: () => void }) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [defaults] = useState(() => {
    const stamp = Date.now();
    return { holderId: `cliente-${stamp}`, number: String(stamp).slice(-8), pixKey: `ana.${stamp}@bankflow.dev` };
  });
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setPending(true); setError(null);
    const data = new FormData(event.currentTarget);
    try {
      await createAccount({ number: String(data.get("number")), holderId: String(data.get("holderId")), holderName: String(data.get("holderName")), document: String(data.get("document")), pixKey: String(data.get("pixKey")), type: String(data.get("type")) as AccountType, initialBalance: Number(data.get("initialBalance")), dailyTransferLimit: Number(data.get("dailyLimit")), nightlyTransferLimit: Number(data.get("nightlyLimit")) });
      onSaved();
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha ao criar conta."); }
    finally { setPending(false); }
  }
  return <form className="bank-form" onSubmit={submit}>
    <label>Nome do titular<input name="holderName" defaultValue="Ana Martins" required minLength={2}/></label>
    <div className="form-grid"><label>Documento<input name="document" defaultValue="12345678901" required/></label><label>ID do cliente<input name="holderId" defaultValue={defaults.holderId} required/></label></div>
    <div className="form-grid"><label>Número da conta<input name="number" defaultValue={defaults.number} required/></label><label>Tipo<select name="type" defaultValue="Checking"><option value="Checking">Conta corrente</option><option value="Savings">Conta poupança</option></select></label></div>
    <label>Chave Pix<input name="pixKey" defaultValue={defaults.pixKey} required/></label>
    <div className="form-grid"><label>Saldo inicial<input name="initialBalance" type="number" min="0" step="0.01" defaultValue="5000" required/></label><label>Limite diário<input name="dailyLimit" type="number" min="0.01" step="0.01" defaultValue="10000" required/></label></div>
    <label>Limite noturno<input name="nightlyLimit" type="number" min="0.01" step="0.01" defaultValue="1000" required/><small>Período demonstrativo: 20h às 6h, horário de Brasília.</small></label>
    {error && <p className="form-error">{error}</p>}<button className="primary-button" disabled={pending}>{pending ? "Abrindo conta…" : "Abrir conta"}</button>
  </form>;
}

function CashInForm({ account, onSaved }: { account: BankAccount; onSaved: () => void }) {
  const [error, setError] = useState<string | null>(null);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    try { await cashIn(account.id, Number(data.get("amount")), String(data.get("description"))); onSaved(); }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha no aporte."); }
  }
  return <form className="bank-form" onSubmit={submit}><div className="account-context"><span>Conta</span><strong>{account.holderName}</strong><small>{account.number} · saldo {money.format(account.balance)}</small></div><label>Valor<input name="amount" type="number" min="0.01" max="50000000" step="0.01" defaultValue="1000" required/></label><label>Descrição<input name="description" defaultValue="Aporte para demonstração" minLength={3} required/></label>{error && <p className="form-error">{error}</p>}<button className="primary-button">Confirmar aporte</button></form>;
}

function SettingsForm({ account, onSaved }: { account: BankAccount; onSaved: () => void }) {
  const [error, setError] = useState<string | null>(null);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    try { await updateAccount(account, { dailyTransferLimit: Number(data.get("daily")), nightlyTransferLimit: Number(data.get("nightly")), status: String(data.get("status")) as AccountStatus }); onSaved(); }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha na atualização."); }
  }
  return <form className="bank-form" onSubmit={submit}><div className="account-context"><strong>{account.holderName}</strong><small>Versão protegida por concorrência otimista</small></div><label>Limite diário<input name="daily" type="number" step="0.01" defaultValue={account.dailyTransferLimit} required/></label><label>Limite noturno<input name="nightly" type="number" step="0.01" defaultValue={account.nightlyTransferLimit} required/></label><label>Situação<select name="status" defaultValue={account.status}><option value="Active">Ativa</option><option value="Blocked">Bloqueada</option><option value="Closed">Encerrada</option></select></label>{error && <p className="form-error">{error}</p>}<button className="primary-button">Salvar proteção</button></form>;
}

function Ledger({ account }: { account: BankAccount }) {
  const [items, setItems] = useState<LedgerEntry[]>([]); const [error, setError] = useState<string | null>(null);
  useEffect(() => { listLedger(account.id).then((result) => setItems(result.items)).catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Falha ao carregar extrato.")); }, [account.id]);
  return <div className="ledger-list">{error && <p className="form-error">{error}</p>}{items.map((entry) => <article key={entry.id}><span className={`ledger-sign ${entry.type.includes("Debit") ? "debit" : "credit"}`}>{entry.type.includes("Debit") ? "−" : "+"}</span><div><strong>{entry.description}</strong><small>{date.format(new Date(entry.occurredAtUtc))} · saldo {money.format(entry.balanceAfter)}</small></div><b>{entry.type.includes("Debit") ? "−" : "+"}{money.format(entry.amount)}</b></article>)}{!error && items.length === 0 && <p>Carregando lançamentos…</p>}</div>;
}

function statusLabel(status: AccountStatus) { return ({ Active: "Ativa", Blocked: "Bloqueada", Closed: "Encerrada" } as const)[status]; }
