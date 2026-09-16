# ADR-001 — Ownership e consistência

**Status:** aceito.

## Decisão

Contas é a fonte de verdade para saldo, limites e ledger. Transferências mantém somente a intenção e o estado do fluxo. Cada domínio possui seu SQL Server e não existem joins entre serviços.

## Consequências

- autonomia de evolução e isolamento de falhas;
- necessidade de consistência eventual e estados pendentes;
- maior custo de observabilidade e operação;
- validações rápidas podem ser repetidas de forma autoritativa na liquidação.
