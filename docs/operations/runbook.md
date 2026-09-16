# Runbook operacional

## Subir e parar

```powershell
.\scripts\bootstrap.ps1 -Start
docker compose ps
docker compose stop
docker desktop stop
```

## Transferência parada

1. Consulte `docker compose ps`.
2. Verifique `docker compose logs transfers accounts rabbitmq --tail 200`.
3. Abra RabbitMQ e confira `accounts.transfer-commands.v1`, `transfers.settlement-results.v1` e DLQs.
4. Preserve `MessageId` e `CorrelationId` durante qualquer reprocessamento.
5. Não altere saldo manualmente no banco.

## Contas ou Transferências degradado

```powershell
docker compose logs accounts-db transfers-db --tail 200
docker compose logs accounts transfers --tail 200
```

Readiness falha quando SQL Server ou RabbitMQ não estão prontos. Liveness indica apenas que o processo está ativo.

## Recuperação

- reinicie somente o serviço afetado;
- não use `docker compose down -v` sem intenção de apagar os dados;
- antes de reprocessar DLQ, corrija a causa e registre a ação;
- confirme saldos pela soma lógica dos lançamentos e pelo saldo materializado.

## SLOs demonstrativos

- Gateway: 99,9% de disponibilidade mensal;
- leituras: p95 abaixo de 500 ms;
- liquidação assíncrona: p95 abaixo de 30 s;
- DLQ: zero mensagem nova sem investigação por mais de 15 minutos.
