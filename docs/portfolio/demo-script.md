# Roteiro de demonstração — 2 a 3 minutos

## 0:00–0:15 · Contexto

Texto na tela: **BankFlow · core banking orientado a eventos**

Mostre o login sem revelar a senha. Explique que é um laboratório autoral, com transações simuladas e identidade própria.

## 0:15–0:35 · Arquitetura viva

Abra a visão geral com os quatro serviços operacionais. Destaque Gateway, Identity, Transferências, Contas, bancos separados, RabbitMQ e OpenTelemetry.

## 0:35–1:05 · Contas e proteção

Abra Contas. Mostre saldo, status ativo/bloqueado, limites diário/noturno e um extrato. Faça um aporte pequeno e mostre o lançamento criado.

## 1:05–1:45 · Pix e consistência eventual

Crie uma transferência entre duas contas. Mostre o estado `Processando` e a mudança automática para `Concluída`. Explique que Transferências grava Outbox, Contas revalida regras, liquida débito/crédito atomicamente e devolve o resultado via eventos.

## 1:45–2:10 · Estorno auditável

Solicite estorno. Mostre que o registro original permanece e que surgem lançamentos compensatórios. Diga que falhas viram estados de negócio, sem apagar a trilha.

## 2:10–2:35 · Cenário de rejeição

Tente transferir acima do saldo ou limite. Mostre o motivo de rejeição e que nenhum saldo foi alterado.

## 2:35–3:00 · Engenharia de produção

Abra Observabilidade, Aspire e RabbitMQ. Termine no README, mostrando testes, ADRs, runbook, CI, Docker e Kubernetes.

Texto final: **Domínio financeiro + confiabilidade + experiência do usuário**
