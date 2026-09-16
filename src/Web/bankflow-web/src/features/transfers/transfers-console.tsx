"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { EyeIcon, PlusIcon, RefreshIcon } from "@/components/icons";
import { SidePanel } from "@/components/side-panel";
import { listAccounts } from "@/features/accounts/accounts-api";
import type { BankAccount, Transfer, TransferMethod, TransferStatus } from "@/lib/contracts";
import { createTransfer, getTransfer, listTransfers, requestReversal } from "./transfers-api";

const money = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const date = new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" });
type Panel = { kind: "create" } | { kind: "details" | "reversal"; transfer: Transfer } | null;

export function TransfersConsole() {
  const [transfers, setTransfers] = useState<Transfer[]>([]);
  const [accounts, setAccounts] = useState<BankAccount[]>([]);
  const [status, setStatus] = useState<TransferStatus | "">("");
  const [panel, setPanel] = useState<Panel>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async (quiet = false) => {
    if (!quiet) setLoading(true);
    try {
      const [transferResult, accountResult] = await Promise.all([listTransfers(status), listAccounts()]);
      setTransfers(transferResult.items); setAccounts(accountResult.items); setError(null);
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha ao carregar transferências."); }
    finally { if (!quiet) setLoading(false); }
  }, [status]);

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);
  useEffect(() => {
    if (!transfers.some((item) => item.status === "PendingProcessing" || item.status === "ReversalPending")) return;
    const timer = window.setInterval(() => void load(true), 2000);
    return () => window.clearInterval(timer);
  }, [load, transfers]);

  const metrics = useMemo(() => ({
    completed: transfers.filter((item) => item.status === "Completed").length,
    pending: transfers.filter((item) => item.status === "PendingProcessing" || item.status === "ReversalPending").length,
    volume: transfers.filter((item) => item.status === "Completed").reduce((sum, item) => sum + item.amount, 0),
    rejected: transfers.filter((item) => item.status === "Rejected" || item.status === "ReversalRejected").length,
  }), [transfers]);
  const byId = useMemo(() => new Map(accounts.map((item) => [item.id, item])), [accounts]);

  function saved(message: string) { setPanel(null); setNotice(message); void load(true); }

  return <main className="bank-page">
    <header className="page-heading bank-heading"><div><span className="eyebrow">Pagamentos · Pix</span><h1>Transferências</h1><p>Acompanhe o ciclo completo da solicitação à liquidação.</p></div><button className="primary-button compact-button" onClick={() => setPanel({ kind: "create" })}><PlusIcon /> Nova transferência</button></header>
    <section className="bank-stats"><article className="purple-stat"><span>Volume concluído</span><strong>{money.format(metrics.volume)}</strong><small>na página atual</small></article><article><span>Concluídas</span><strong>{metrics.completed}</strong><small>liquidação confirmada</small></article><article><span>Em processamento</span><strong>{metrics.pending}</strong><small>atualização automática</small></article><article><span>Rejeitadas</span><strong>{metrics.rejected}</strong><small>com motivo auditável</small></article></section>
    {notice && <div className="bank-message success">{notice}<button onClick={() => setNotice(null)}>×</button></div>}{error && <div className="bank-message error">{error}</div>}
    <section className="data-card"><div className="bank-toolbar"><select value={status} onChange={(event) => setStatus(event.target.value as TransferStatus | "")}><option value="">Todas as situações</option>{Object.entries(statusLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select><button className="icon-button" onClick={() => void load()} aria-label="Atualizar"><RefreshIcon /></button></div>
      <div className="table-scroll"><table className="bank-table"><thead><tr><th>Transferência</th><th>Origem → destino</th><th>Método</th><th>Situação</th><th>Valor</th><th>Ações</th></tr></thead><tbody>{transfers.map((transfer) => <tr key={transfer.id}><td><strong>{transfer.number}</strong><small>{date.format(new Date(transfer.createdAtUtc))} · {transfer.externalReference}</small></td><td><strong>{byId.get(transfer.sourceAccountId)?.holderName ?? shortId(transfer.sourceAccountId)} → {byId.get(transfer.destinationAccountId)?.holderName ?? shortId(transfer.destinationAccountId)}</strong><small>{transfer.description}</small></td><td>{transfer.method}</td><td><span className={`status-pill ${transfer.status.toLowerCase()}`}>{statusLabels[transfer.status]}</span>{transfer.statusReason && <small>{transfer.statusReason}</small>}</td><td className="money-cell">{money.format(transfer.amount)}</td><td><div className="row-actions"><button onClick={() => setPanel({ kind: "details", transfer })}><EyeIcon /> Detalhes</button>{canReverse(transfer.status) && <button onClick={() => setPanel({ kind: "reversal", transfer })}>Estornar</button>}</div></td></tr>)}</tbody></table></div>
      {!loading && transfers.length === 0 && <div className="empty-bank">Nenhuma transferência encontrada. Crie uma para demonstrar o fluxo assíncrono.</div>}{loading && <div className="empty-bank">Carregando transferências…</div>}
    </section>
    <aside className="flow-note"><strong>Liquidação confiável</strong><span>API → Outbox → RabbitMQ → débito/crédito atômicos → Inbox → atualização de status</span></aside>
    {panel?.kind === "create" && <SidePanel eyebrow="Pagamento instantâneo" title="Nova transferência" onClose={() => setPanel(null)}><TransferForm accounts={accounts} onSaved={() => saved("Transferência aceita e enviada para liquidação.")} /></SidePanel>}
    {panel?.kind === "details" && <SidePanel eyebrow="Rastreabilidade" title="Detalhes da transferência" onClose={() => setPanel(null)}><TransferDetails transfer={panel.transfer} accounts={byId} onRefresh={async () => { const updated = await getTransfer(panel.transfer.id); setPanel({ kind: "details", transfer: updated }); void load(true); }} /></SidePanel>}
    {panel?.kind === "reversal" && <SidePanel eyebrow="Operação compensatória" title="Solicitar estorno" onClose={() => setPanel(null)}><ReversalForm transfer={panel.transfer} onSaved={() => saved("Estorno solicitado para processamento assíncrono.")} /></SidePanel>}
  </main>;
}

function TransferForm({ accounts, onSaved }: { accounts: BankAccount[]; onSaved: () => void }) {
  const active = accounts.filter((item) => item.status === "Active");
  const [source, setSource] = useState(active[0]?.id ?? "");
  const [destination, setDestination] = useState(active.find((item) => item.id !== active[0]?.id)?.id ?? "");
  const [reference] = useState(() => `PIX-${Date.now()}`);
  const [error, setError] = useState<string | null>(null); const [pending, setPending] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setPending(true); setError(null); const data = new FormData(event.currentTarget);
    try { await createTransfer({ sourceAccountId: source, destinationAccountId: destination, externalReference: String(data.get("reference")), method: String(data.get("method")) as TransferMethod, amount: Number(data.get("amount")), description: String(data.get("description")) }); onSaved(); }
    catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha ao criar transferência."); }
    finally { setPending(false); }
  }
  return <form className="bank-form" onSubmit={submit}><label>Conta de origem<select value={source} onChange={(event) => setSource(event.target.value)} required>{active.map((account) => <option key={account.id} value={account.id}>{account.holderName} · {money.format(account.balance)}</option>)}</select></label><label>Conta de destino<select value={destination} onChange={(event) => setDestination(event.target.value)} required>{accounts.filter((item) => item.id !== source && item.status !== "Closed").map((account) => <option key={account.id} value={account.id}>{account.holderName} · {account.pixKey}</option>)}</select></label><div className="form-grid"><label>Método<select name="method" defaultValue="Pix"><option>Pix</option><option value="Internal">Interna</option><option>Ted</option></select></label><label>Valor<input name="amount" type="number" min="0.01" max="50000000" step="0.01" defaultValue="250" required/></label></div><label>Referência idempotente<input name="reference" defaultValue={reference} minLength={3} required/><small>Repetir a mesma referência e conteúdo não duplica a transferência.</small></label><label>Descrição<input name="description" defaultValue="Pagamento de demonstração" minLength={3} required/></label>{error && <p className="form-error">{error}</p>}<button className="primary-button" disabled={pending || active.length < 2}>{pending ? "Enviando…" : "Confirmar transferência"}</button></form>;
}

function TransferDetails({ transfer, accounts, onRefresh }: { transfer: Transfer; accounts: Map<string, BankAccount>; onRefresh: () => Promise<void> }) {
  return <div className="transfer-details"><div className="transfer-hero"><span className={`status-pill ${transfer.status.toLowerCase()}`}>{statusLabels[transfer.status]}</span><strong>{money.format(transfer.amount)}</strong><small>{transfer.method} · {transfer.externalReference}</small></div><dl><div><dt>Origem</dt><dd>{accounts.get(transfer.sourceAccountId)?.holderName ?? transfer.sourceAccountId}</dd></div><div><dt>Destino</dt><dd>{accounts.get(transfer.destinationAccountId)?.holderName ?? transfer.destinationAccountId}</dd></div><div><dt>Criada em</dt><dd>{date.format(new Date(transfer.createdAtUtc))}</dd></div><div><dt>Descrição</dt><dd>{transfer.description}</dd></div>{transfer.statusReason && <div><dt>Resultado</dt><dd>{transfer.statusReason}</dd></div>}</dl><button className="secondary-button" onClick={() => void onRefresh()}><RefreshIcon /> Atualizar situação</button></div>;
}

function ReversalForm({ transfer, onSaved }: { transfer: Transfer; onSaved: () => void }) {
  const [error, setError] = useState<string | null>(null);
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const data = new FormData(event.currentTarget); try { await requestReversal(transfer, String(data.get("reason"))); onSaved(); } catch (requestError) { setError(requestError instanceof Error ? requestError.message : "Falha ao solicitar estorno."); } }
  return <form className="bank-form" onSubmit={submit}><div className="account-context"><span>Transferência</span><strong>{transfer.number} · {money.format(transfer.amount)}</strong><small>O estorno cria lançamentos compensatórios; o histórico original não é apagado.</small></div><label>Motivo<textarea name="reason" defaultValue="Solicitação de estorno para demonstração" minLength={3} maxLength={300} required/></label>{error && <p className="form-error">{error}</p>}<button className="danger-button">Solicitar estorno</button></form>;
}

const statusLabels: Record<TransferStatus, string> = { PendingProcessing: "Processando", Completed: "Concluída", Rejected: "Rejeitada", ReversalPending: "Estorno pendente", Reversed: "Estornada", ReversalRejected: "Estorno rejeitado" };
function canReverse(status: TransferStatus) { return status === "Completed" || status === "ReversalRejected"; }
function shortId(value: string) { return `${value.slice(0, 8)}…`; }
