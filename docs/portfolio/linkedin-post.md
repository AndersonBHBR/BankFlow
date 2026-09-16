# Texto sugerido para LinkedIn

Transformei uma arquitetura de comércio em um laboratório autoral de core banking: o **BankFlow**.

O objetivo não foi apenas criar uma interface de banco digital, mas modelar os problemas que aparecem por trás de uma transferência financeira: ownership de dados, consistência eventual, idempotência, concorrência, auditoria e recuperação de falhas.

O projeto demonstra:

• contas digitais com saldo, bloqueio e limites diário/noturno;
• Pix e transferências com validação em duas etapas;
• liquidação atômica com pares de débito e crédito;
• razão imutável e saldo após cada lançamento;
• estorno por lançamentos compensatórios, sem apagar o histórico;
• RabbitMQ com Outbox, Inbox, retry e DLQ;
• JWT, API Gateway, BFF e cookie HttpOnly;
• OpenTelemetry, Aspire Dashboard e health checks;
• Docker Compose, Kubernetes, CI, testes unitários e de arquitetura;
• portal responsivo em Next.js 16 e serviços em .NET 10.

Uma decisão importante foi manter o serviço de Contas como única fonte de verdade do saldo. Transferências controla o fluxo, mas toda liquidação é revalidada e executada no domínio financeiro. Assim, uma consulta rápida pode melhorar a experiência sem comprometer a regra autoritativa.

Também documentei os limites conscientes do laboratório e o que seria necessário para produção: KYC/AML, antifraude, autenticação forte, compliance, Pix real, reconciliação e proteção de dados.

O BankFlow é um projeto educacional independente, com transações simuladas e sem vínculo com qualquer instituição financeira.

Repositório: [INSIRA O LINK]

#dotnet #nextjs #microservices #eventdriven #rabbitmq #docker #kubernetes #opentelemetry #fintech #softwareengineering
