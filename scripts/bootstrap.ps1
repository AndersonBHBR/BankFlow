[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $root '.env'

function New-RandomBytes([int]$Length) {
    $bytes = New-Object byte[] $Length
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return ,$bytes
    }
    finally {
        $generator.Dispose()
    }
}

if ((Test-Path $envPath) -and -not $Force) {
    Write-Host '.env já existe. Use -Force apenas se quiser substituir os segredos locais.' -ForegroundColor Yellow
}
else {
    $jwtKey = [Convert]::ToBase64String((New-RandomBytes 48))
    $sqlRandomPart = -join ((New-RandomBytes 12) | ForEach-Object { $_.ToString('x2') })
    $sqlPassword = 'Bf!2026-' + $sqlRandomPart + 'aA1!'
    $rabbitPassword = [Convert]::ToBase64String((New-RandomBytes 24))

    $lines = @(
        "JWT_SIGNING_KEY=$jwtKey"
        "SQL_SA_PASSWORD=$sqlPassword"
        'RABBITMQ_USER=bankflow'
        "RABBITMQ_PASSWORD=$rabbitPassword"
    )

    [IO.File]::WriteAllLines($envPath, $lines)
    Write-Host '.env criado com valores aleatórios.' -ForegroundColor Green
}

if ($Start) {
    Push-Location $root
    try {
        docker compose up --build -d
        docker compose ps
    }
    finally {
        Pop-Location
    }
}
