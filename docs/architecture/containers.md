# Arquitetura de contêineres

| Contêiner | Responsabilidade | Porta local | Dependências |
|---|---|---:|---|
| `admin` | Next.js, UI e BFF | 3000 | Gateway |
| `gateway` | roteamento, JWT e rate limiting | 8080 | APIs |
| `identity` | login demonstrativo e JWT | 8081 | — |
| `transfers` | ciclo de vida de transferências | 8082 | SQL, Contas, RabbitMQ |
| `accounts` | contas, limites, saldo e ledger | 8083 | SQL, RabbitMQ |
| `transfers-db` | ownership de Transferências | 14331 | — |
| `accounts-db` | ownership de Contas | 14332 | — |
| `rabbitmq` | eventos, comandos e DLQ | 5672/15672 | — |
| `aspire-dashboard` | traces, métricas e logs | 18888 | OTLP |

## Fronteiras

- Transferências conhece Contas somente por HTTP e contratos de integração.
- Contas não referencia assemblies de Transferências.
- nenhum serviço consulta o banco do outro.
- o Gateway não contém regra de domínio.
- o BFF mantém o JWT fora do JavaScript do navegador.

## Consistência

A transferência aceita pela API inicia em `PendingProcessing`. A liquidação é eventualmente consistente entre bancos separados; dentro do banco de Contas, débito, crédito, ledger, Inbox e Outbox são atômicos.
