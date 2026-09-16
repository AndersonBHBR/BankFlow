[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-NativeCommandSucceeded([string]$Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step falhou com o código de saída $LASTEXITCODE."
    }
}

Push-Location $root
try {
    Write-Host '1/7 - Restaurando .NET...' -ForegroundColor Cyan
    dotnet restore BankFlow.slnx -p:NuGetAudit=true -p:NuGetAuditMode=all
    Assert-NativeCommandSucceeded 'Restauração .NET'

    Write-Host '2/7 - Compilando .NET...' -ForegroundColor Cyan
    dotnet build BankFlow.slnx --configuration Release --no-restore
    Assert-NativeCommandSucceeded 'Compilação .NET'

    Write-Host '3/7 - Executando testes...' -ForegroundColor Cyan
    dotnet test --solution BankFlow.slnx --configuration Release --no-build --results-directory TestResults
    Assert-NativeCommandSucceeded 'Testes .NET'

    Write-Host '4/7 - Instalando portal...' -ForegroundColor Cyan
    Push-Location 'src/Web/bankflow-web'
    try {
        npm ci
        Assert-NativeCommandSucceeded 'Instalação do portal'
        Write-Host '5/7 - Auditando e analisando portal...' -ForegroundColor Cyan
        npm audit --omit=dev --audit-level=high
        Assert-NativeCommandSucceeded 'Auditoria do portal'
        npm run lint
        Assert-NativeCommandSucceeded 'Lint do portal'
        Write-Host '6/7 - Compilando portal...' -ForegroundColor Cyan
        npm run build
        Assert-NativeCommandSucceeded 'Compilação do portal'
    }
    finally {
        Pop-Location
    }

    Write-Host '7/7 - Validando Docker Compose...' -ForegroundColor Cyan
    docker compose --env-file .env.example config --quiet
    Assert-NativeCommandSucceeded 'Validação do Docker Compose'
    Write-Host 'BankFlow aprovado por todas as validações.' -ForegroundColor Green
}
finally {
    Pop-Location
}
