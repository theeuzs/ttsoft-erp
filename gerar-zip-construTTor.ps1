# Gera ZIP do ConstruTTor excluindo bin, obj, .git e lixo de build
# Execute de C:\ERP\ERP:
#   PowerShell -ExecutionPolicy Bypass -File .\gerar-zip-construTTor.ps1

$raiz    = Get-Location
$destino = "C:\ERP\ConstruTTor_Source.zip"

$pastaExcluir = @("bin","obj",".git",".vs",".vscode","node_modules","publish","TestResults","Logs",".github")
$extExcluir   = @(".user",".suo",".bak",".nupkg")

if (Test-Path $destino) { Remove-Item $destino -Force }

$arquivos = Get-ChildItem -Path $raiz -Recurse -File | Where-Object {
    $p   = $_.FullName
    $ext = $_.Extension.ToLower()

    $bloqueadoPasta = $pastaExcluir | Where-Object { $p -match [regex]::Escape("\$_\") } | Select-Object -First 1
    $bloqueadoExt   = $extExcluir   | Where-Object { $ext -eq $_ }                       | Select-Object -First 1

    (-not $bloqueadoPasta) -and (-not $bloqueadoExt)
}

Compress-Archive -Path $arquivos.FullName -DestinationPath $destino
$mb = (Get-Item $destino).Length / 1MB
Write-Host "Pronto: $destino  ($($mb.ToString('N1')) MB | $($arquivos.Count) arquivos)"
