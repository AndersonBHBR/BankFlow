# ADR-002 — Outbox, Inbox e idempotência

**Status:** aceito.

## Decisão

Persistir evento e agregado na mesma transação local (Outbox), publicar no RabbitMQ e registrar `MessageId` antes de reconhecer o consumo (Inbox). A entrega é pelo menos uma vez.

## Consequências

- não existe janela entre commit e publicação;
- consumidores precisam ser idempotentes;
- duplicidade é esperada e tratada;
- DLQ, métricas e reprocessamento operacional são obrigatórios.
