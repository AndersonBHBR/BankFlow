# Segurança

BankFlow é um laboratório e não deve processar dinheiro ou dados pessoais reais.

Para relatar uma vulnerabilidade, não abra uma issue pública com credenciais ou dados sensíveis. Use um canal privado do mantenedor. Inclua versão, cenário, impacto e passos mínimos de reprodução.

## Antes de expor o projeto

- substitua usuários demonstrativos por um provedor de identidade;
- mova todos os segredos para um cofre e habilite rotação;
- use TLS ponta a ponta e `BANKFLOW_COOKIE_SECURE=true`;
- aplique criptografia/tokenização para PII;
- revise CORS, CSP, rate limits e acesso ao RabbitMQ/Aspire;
- execute SAST, análise de dependências, DAST e threat modeling;
- não publique `.env`, dumps, tokens ou logs com dados pessoais.
