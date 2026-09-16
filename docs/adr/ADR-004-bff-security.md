# ADR-004 — Segurança do portal BFF

**Status:** aceito.

## Decisão

O navegador autentica pelo BFF Next.js. O token JWT fica em cookie `HttpOnly`, `SameSite=Lax` e `Secure` em produção. Mutações exigem mesma origem usando `Origin`, `Host`, `X-Forwarded-Host` e `X-Forwarded-Proto`.

## Consequências

- scripts do cliente não acessam o token;
- reduz exposição a XSS e CSRF;
- proxy reverso precisa encaminhar host e protocolo corretamente;
- CSP e demais headers são aplicados pelo Next.js.
