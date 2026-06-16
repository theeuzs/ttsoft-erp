# 1.8.1 + 1.8.2 - RBAC Patch
# Execute de C:\ERP\ERP:
#   PowerShell -ExecutionPolicy Bypass -File .\rbac-patch-18.ps1

$base    = Join-Path (Get-Location) "ERP.Api\Controllers"
$applied = 0
$missed  = 0

function Patch {
    param([string]$File, [string]$HttpAttr, [string]$Perm)
    $path = Join-Path $base $File
    if (-not (Test-Path $path)) { Write-Host "  NOT_FOUND  $File"; return }
    $content  = [System.IO.File]::ReadAllText($path)
    $permAttr = "[HasPermission(Permissions.$Perm)]"
    $needle   = "    $HttpAttr"
    $chkC = "    $permAttr" + "`r`n    $HttpAttr"
    $chkL = "    $permAttr" + "`n    $HttpAttr"
    if ($content.Contains($chkC) -or $content.Contains($chkL)) { $script:applied++; return }
    $repC = "    $permAttr" + "`r`n    $HttpAttr"
    $repL = "    $permAttr" + "`n    $HttpAttr"
    $upd = $content.Replace($needle, $repC)
    if ($upd -eq $content) { $upd = $content.Replace($needle, $repL) }
    if ($upd -ne $content) {
        [System.IO.File]::WriteAllText($path, $upd)
        $script:applied++
        Write-Host "  OK  $File -> $Perm"
    } else {
        $script:missed++
        Write-Host "  ERR ${File}: nao encontrado: $HttpAttr"
    }
}

Write-Host ""
Write-Host "=== 1.8.1 Bloqueadores ==="
Write-Host "CustomersController"
Patch "CustomersController.cs"  '[HttpPost]'               "CustomerEdit"
Patch "CustomersController.cs"  '[HttpPut("{id:guid}")]'   "CustomerEdit"
Patch "CustomersController.cs"  '[HttpDelete("{id:guid}")]' "CustomerDelete"
Write-Host "FidelidadeController"
Patch "FidelidadeController.cs" '[HttpPost("{customerId:guid}/resgatar")]' "FidelidadeUse"

Write-Host ""
Write-Host "=== 1.8.2 RBAC restante ==="
Write-Host "BiController"
Patch "BiController.cs" '[HttpGet("sazonalidade")]' "ReportFinancial"
Patch "BiController.cs" '[HttpGet("abc")]' "ReportFinancial"
Patch "BiController.cs" '[HttpGet("dre-detalhado")]' "ReportFinancial"
Patch "BiController.cs" '[HttpGet("ranking-vendedores")]' "ReportFinancial"
Patch "BiController.cs" '[HttpGet("previsao-demanda")]' "ReportFinancial"
Write-Host "ComissaoController"
Patch "ComissaoController.cs" '[HttpGet]' "ReportFinancial"
Write-Host "EntregasController"
Patch "EntregasController.cs" '[HttpDelete("{id:guid}")]' "EntregasManage"
Patch "EntregasController.cs" '[HttpPut("{id:guid}/status")]' "EntregasManage"
Patch "EntregasController.cs" '[HttpPut("{id:guid}/motorista")]' "EntregasManage"
Write-Host "StorageController"
Patch "StorageController.cs" '[HttpPost("produto/{produtoId:guid}/imagem")]' "ProductEdit"
Patch "StorageController.cs" '[HttpDelete("produto/{produtoId:guid}/imagem")]' "ProductEdit"

Write-Host ""
Write-Host "=== 1.8.3 RequestSizeLimit ==="
$sp = Join-Path $base "StorageController.cs"
$sc = [System.IO.File]::ReadAllText($sp)
foreach ($u in @('[HttpPost("produto/{produtoId:guid}/imagem")]', '[HttpPost("entrega/{entregaId:guid}/foto")]')) {
    $lim = "[RequestSizeLimit(5_242_880)]"
    $cC  = "    $lim" + "`r`n    $u"
    $cL  = "    $lim" + "`n    $u"
    if (-not ($sc.Contains($cC) -or $sc.Contains($cL))) {
        $sc = $sc.Replace("    $u", "    $lim`r`n    $u")
        Write-Host "  OK  StorageController -> RequestSizeLimit"
        $script:applied++
    }
}
[System.IO.File]::WriteAllText($sp, $sc)

Write-Host ""
Write-Host "=== MANUAL (ver instrucoes abaixo) ==="
Write-Host "  AuditoriaController: [Authorize(Roles=Administrador)] -> [HasPermission(Permissions.AuditView)]"
Write-Host "  MetricsController:   [Authorize] -> [Authorize(Roles = Administrador)]"
Write-Host "  appsettings.json:    4 valores Marketplace -> CONFIGURADO_VIA_AZURE_APP_SERVICE"
Write-Host ""
Write-Host "=== Concluido: $applied aplicados, $missed nao encontrados ==="
Write-Host "Proximo: dotnet test ERP.Tests"