# ADR-003 — Ledger e estornos compensatórios

**Status:** aceito.

## Decisão

Toda movimentação gera lançamento imutável. Transferências geram pares débito/crédito; estornos geram novos pares no sentido inverso. O lançamento original nunca é alterado.

## Consequências

- trilha completa e reconciliável;
- falha do estorno por saldo insuficiente é visível como estado de negócio;
- correções operacionais não apagam evidência;
- um ledger real exigiria controles contábeis e reconciliação adicionais.
