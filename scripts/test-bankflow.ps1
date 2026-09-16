[CmdletBinding()]
param([string]$BaseUrl = 'http://localhost:8080')

$ErrorActionPreference = 'Stop'

Write-Host '1/7 - Autenticando...' -ForegroundColor Cyan
$token = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/auth/token" -ContentType 'application/json' -Body (@{
    login = 'admin@bankflow.local'
    password = 'BankFlow#2026'
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($token.accessToken)" }
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

function New-DemoAccount([string]$Suffix, [decimal]$Balance) {
    $body = @{
        number = "$stamp".Substring("$stamp".Length - 7) + $Suffix
        holderId = "smoke-$stamp-$Suffix"
        holderName = "Cliente Smoke $Suffix"
        document = if ($Suffix -eq '1') { '12345678901' } else { '98765432100' }
        pixKey = "smoke-$stamp-$Suffix@bankflow.dev"
        type = 'Checking'
        initialBalance = $Balance
        dailyTransferLimit = 10000
        nightlyTransferLimit = 10000
    } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/accounts" -Headers $headers -ContentType 'application/json' -Body $body
}

Write-Host '2/7 - Abrindo contas...' -ForegroundColor Cyan
$source = New-DemoAccount '1' 1000
$destination = New-DemoAccount '2' 100

Write-Host '3/7 - Criando transferência idempotente...' -ForegroundColor Cyan
$transferBody = @{
    sourceAccountId = $source.id
    destinationAccountId = $destination.id
    externalReference = "SMOKE-$stamp"
    method = 'Pix'
    amount = 250
    description = 'Teste funcional BankFlow'
} | ConvertTo-Json
$transfer = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/transfers" -Headers $headers -ContentType 'application/json' -Body $transferBody
$replay = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/transfers" -Headers $headers -ContentType 'application/json' -Body $transferBody
if ($replay.id -ne $transfer.id) { throw 'A referência idempotente criou outra transferência.' }

Write-Host '4/7 - Aguardando liquidação...' -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 500
    $current = Invoke-RestMethod -Uri "$BaseUrl/api/v1/transfers/$($transfer.id)" -Headers $headers
} while ($current.status -eq 'PendingProcessing' -and (Get-Date) -lt $deadline)
if ($current.status -ne 'Completed') { throw "Situação inesperada: $($current.status) - $($current.statusReason)" }

Write-Host '5/7 - Validando débito e crédito...' -ForegroundColor Cyan
$sourceAfter = Invoke-RestMethod -Uri "$BaseUrl/api/v1/accounts/$($source.id)" -Headers $headers
$destinationAfter = Invoke-RestMethod -Uri "$BaseUrl/api/v1/accounts/$($destination.id)" -Headers $headers
if ($sourceAfter.balance -ne 750 -or $destinationAfter.balance -ne 350) { throw 'Os saldos da liquidação estão incorretos.' }

Write-Host '6/7 - Solicitando estorno...' -ForegroundColor Cyan
$reversal = @{ rowVersion = $current.rowVersion; reason = 'Estorno do teste funcional' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/transfers/$($transfer.id)/reversal" -Headers $headers -ContentType 'application/json' -Body $reversal | Out-Null
do {
    Start-Sleep -Milliseconds 500
    $current = Invoke-RestMethod -Uri "$BaseUrl/api/v1/transfers/$($transfer.id)" -Headers $headers
} while ($current.status -eq 'ReversalPending' -and (Get-Date) -lt $deadline.AddSeconds(30))
if ($current.status -ne 'Reversed') { throw "O estorno terminou em $($current.status)." }

Write-Host '7/7 - Validando lançamentos compensatórios...' -ForegroundColor Cyan
$ledger = Invoke-RestMethod -Uri "$BaseUrl/api/v1/accounts/$($source.id)/ledger?page=1&pageSize=20" -Headers $headers
if ($ledger.items.Count -lt 3) { throw 'O extrato não contém aporte, débito e crédito de estorno.' }

Write-Host 'BankFlow aprovado: idempotência, liquidação, saldos, ledger e estorno.' -ForegroundColor Green
