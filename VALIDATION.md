# Evidências de validação

## Executado no ambiente de preparação

- `npm audit --omit=dev --audit-level=high`: 0 vulnerabilidades;
- `npm run lint`: aprovado sem erros;
- `npm run build`: aprovado com Next.js 16.3.5 e TypeScript;
- 11 páginas/rotas geradas;
- JSON de Gateway e Identity analisado;
- Docker Compose e manifests Kubernetes analisados como YAML;
- todos os projetos e referências da solução analisados como XML;
- auditoria de delimitadores e referências legadas no código C# aprovada.

## Validação necessária em máquina com .NET/Docker

O ambiente de preparação não disponibilizava `dotnet` nem Docker. Execute:

```powershell
.\scripts\validate.ps1
```

Depois inicie a plataforma e execute:

```powershell
.\scripts\bootstrap.ps1 -Start
.\scripts\test-bankflow.ps1
```

Não publique como release antes de essas duas etapas passarem no seu ambiente e no GitHub Actions.
