# 1.7.7 - RBAC Patch: HasPermission nos 16 controllers restantes
# Execute de C:\ERP\ERP:
#   PowerShell -ExecutionPolicy Bypass -File .\rbac-patch.ps1

$base    = Join-Path (Get-Location) "ERP.Api\Controllers"
$applied = 0
$missed  = 0

function Patch {
    param(
        [string]$File,
        [string]$HttpAttr,
        [string]$Perm
    )

    $path = Join-Path $base $File
    if (-not (Test-Path $path)) {
        Write-Host "  NOT_FOUND  $File"
        return
    }

    $content = [System.IO.File]::ReadAllText($path)
    $permAttr = "[HasPermission(Permissions.$Perm)]"
    $needle   = "    $HttpAttr"

    # Idempotente
    $checkCRLF = "    $permAttr" + "`r`n    $HttpAttr"
    $checkLF   = "    $permAttr" + "`n    $HttpAttr"
    if ($content.Contains($checkCRLF) -or $content.Contains($checkLF)) {
        $script:applied++
        return
    }

    # Insere o atributo antes do HttpXxx
    $replaceCRLF = "    $permAttr" + "`r`n    $HttpAttr"
    $replaceLF   = "    $permAttr" + "`n    $HttpAttr"

    $updated = $content.Replace($needle, $replaceCRLF)
    if ($updated -eq $content) {
        $updated = $content.Replace($needle, $replaceLF)
    }

    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($path, $updated)
        $script:applied++
        Write-Host "  OK  $File  ->  $Perm  antes de  $HttpAttr"
    } else {
        $script:missed++
        Write-Host "  ERR ${File}: padrao nao encontrado: $HttpAttr"
    }
}

Write-Host ""
Write-Host "=== 1.7.7 RBAC Patch: 16 controllers, 34 atributos ==="
Write-Host ""

Write-Host "ConciliacaoController"
Patch "ConciliacaoController.cs"  '[HttpPost("importar-extrato")]'  "FinanceiroView"

Write-Host "ConfigController"
Patch "ConfigController.cs"  '[HttpPut]'  "ConfigView"

Write-Host "ContasPagarController"
Patch "ContasPagarController.cs"  '[HttpPost]'                       "DespesasView"
Patch "ContasPagarController.cs"  '[HttpPost("{id:guid}/pagar")]'    "DespesasView"
Patch "ContasPagarController.cs"  '[HttpPost("{id:guid}/cancelar")]' "DespesasView"

Write-Host "ContasReceberController"
Patch "ContasReceberController.cs"  '[HttpPost("parcelar")]'                "FinanceiroView"
Patch "ContasReceberController.cs"  '[HttpPost("{id:guid}/baixa-total")]'   "FinanceiroView"
Patch "ContasReceberController.cs"  '[HttpPost("{id:guid}/baixa-parcial")]' "FinanceiroView"
Patch "ContasReceberController.cs"  '[HttpPost("{id:guid}/gerar-boleto")]'  "FinanceiroView"

Write-Host "DevolucaoController"
Patch "DevolucaoController.cs"  '[HttpPost]'  "SaleReturn"

Write-Host "FilialController"
Patch "FilialController.cs"  '[HttpPost("transferencias")]'                     "ConfigView"
Patch "FilialController.cs"  '[HttpPost("transferencias/{id:guid}/confirmar")]' "ConfigView"
Patch "FilialController.cs"  '[HttpPost("transferencias/{id:guid}/cancelar")]'  "ConfigView"

Write-Host "FiscalController"
Patch "FiscalController.cs"  '[HttpPost("nfse/emitir")]'                 "NotasFiscaisView"
Patch "FiscalController.cs"  '[HttpDelete("nfse/{referencia}/cancelar")]' "NotasFiscaisView"
Patch "FiscalController.cs"  '[HttpPost("calcular-impostos")]'           "NotasFiscaisView"

Write-Host "HaverController"
Patch "HaverController.cs"  '[HttpPost("credito")]'  "HaverEdit"
Patch "HaverController.cs"  '[HttpPost("debito")]'   "HaverEdit"

Write-Host "InventarioController"
Patch "InventarioController.cs"  '[HttpPost("contar")]'               "InventarioView"
Patch "InventarioController.cs"  '[HttpPost("aplicar-ajustes")]'      "StockAdjust"
Patch "InventarioController.cs"  '[HttpPost("relatorio-divergencias")]' "InventarioView"

Write-Host "MetasController"
Patch "MetasController.cs"  '[HttpPost]'               "ReportFinancial"
Patch "MetasController.cs"  '[HttpDelete("{id:guid}")]' "ReportFinancial"

Write-Host "NotasFiscaisController"
Patch "NotasFiscaisController.cs"  '[HttpPost("nfce/emitir")]'         "NotasFiscaisView"
Patch "NotasFiscaisController.cs"  '[HttpPost("{referencia}/cancelar")]' "NotasFiscaisView"

Write-Host "PedidosCompraController"
Patch "PedidosCompraController.cs"  '[HttpPost("{id:guid}/receber")]'  "ComprasView"
Patch "PedidosCompraController.cs"  '[HttpPost("{id:guid}/cancelar")]' "ComprasView"

Write-Host "StockController"
Patch "StockController.cs"  '[HttpPost("adjust")]'  "StockAdjust"

Write-Host "SugestaoComprasController"
Patch "SugestaoComprasController.cs"  '[HttpPost("gerar-pedido")]'  "ComprasView"

Write-Host "TintometricoController"
Patch "TintometricoController.cs"  '[HttpPost]'                             "ProductEdit"
Patch "TintometricoController.cs"  '[HttpDelete("produto/{productId:guid}")]' "ProductEdit"

Write-Host "TransferenciasController"
Patch "TransferenciasController.cs"  '[HttpPost]'                      "FinanceiroView"
Patch "TransferenciasController.cs"  '[HttpPut("{id:guid}/confirmar")]' "FinanceiroView"
Patch "TransferenciasController.cs"  '[HttpPut("{id:guid}/cancelar")]'  "FinanceiroView"

Write-Host ""
Write-Host "=== Concluido: $applied aplicados, $missed nao encontrados ==="
Write-Host ""
Write-Host "Proximo passo:"
Write-Host "  dotnet test ERP.Tests --logger `"console;verbosity=normal`""
Write-Host ""
