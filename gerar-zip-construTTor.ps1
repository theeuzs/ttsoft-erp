# Gera ZIP do ConstruTTor para auditoria externa — exclui secrets, credenciais e lixo de build.
# Execute de C:\ERP\ERP:
#   PowerShell -ExecutionPolicy Bypass -File .\gerar-zip-construTTor.ps1

$raiz    = Get-Location
$destino = "C:\ERP\ConstruTTor_Source.zip"

# ── Pastas excluídas (qualquer segmento do path) ────────────────────────────
$pastaExcluir = @(
    "bin", "obj", ".git", ".vs", ".vscode",
    "node_modules", "publish", "TestResults",
    "Logs",          # logs de produção/staging
    ".github"
)

# ── Extensões excluídas ─────────────────────────────────────────────────────
$extExcluir = @(".user", ".suo", ".bak", ".nupkg", ".pfx", ".p12", ".key")

# ── Arquivos específicos excluídos (nome exato, case-insensitive) ────────────
# Qualquer arquivo nesses nomes é bloqueado independente da pasta onde estiver.
$arquivoExcluir = @(
    "config_recibo.json",           # PII cliente (endereço, telefone, token NFe)
    "google-credentials.json",      # SA GCP com chave privada PEM
    "appsettings.Development.json", # connection string do banco de staging
    "appsettings.Staging.json",     # idem
    "licenca.json",                 # dados de licença do cliente piloto
    "launchSettings.json",          # ports e env vars locais — não relevante externamente
    "gerar-zip-construTTor.ps1"     # este próprio script — não precisa ir no ZIP
)

# ── Padrões de nome excluídos (wildcard) ────────────────────────────────────
$nomeWildcard = @(
    "*-credentials.json",   # qualquer outro credentials
    "*-secrets.json",       # idem para secrets
    "api-*.json"            # arquivos de log do Serilog (api-20260622.json etc.)
)

if (Test-Path $destino) { Remove-Item $destino -Force }

$sep = [System.IO.Path]::DirectorySeparatorChar

$arquivos = Get-ChildItem -Path $raiz -Recurse -File | Where-Object {
    $arquivo = $_
    $nome    = $arquivo.Name
    $ext     = $arquivo.Extension.ToLower()

    # Verifica pastas — split nos separadores, checa cada segmento
    $segmentos       = $arquivo.FullName -split [regex]::Escape($sep)
    $bloqueadoPasta  = $segmentos | Where-Object { $pastaExcluir -icontains $_ } | Select-Object -First 1

    # Verifica extensão
    $bloqueadoExt    = $extExcluir | Where-Object { $ext -eq $_ } | Select-Object -First 1

    # Verifica nome exato
    $bloqueadoNome   = $arquivoExcluir | Where-Object { $nome -ieq $_ } | Select-Object -First 1

    # Verifica wildcard de nome
    $bloqueadoWild   = $nomeWildcard | Where-Object { $nome -ilike $_ } | Select-Object -First 1

    (-not $bloqueadoPasta) -and (-not $bloqueadoExt) -and (-not $bloqueadoNome) -and (-not $bloqueadoWild)
}

Compress-Archive -Path $arquivos.FullName -DestinationPath $destino
$mb = (Get-Item $destino).Length / 1MB
Write-Host "Pronto: $destino  ($($mb.ToString('N1')) MB | $($arquivos.Count) arquivos)"

# Confirmação de segurança — lista o que NÃO entrou por ser sensível
Write-Host "`n── Arquivos sensíveis EXCLUÍDOS (confirmação) ──────────────────────"
Get-ChildItem -Path $raiz -Recurse -File | Where-Object {
    $nome = $_.Name
    ($arquivoExcluir | Where-Object { $nome -ieq $_ } | Select-Object -First 1) -or
    ($nomeWildcard   | Where-Object { $nome -ilike $_ } | Select-Object -First 1)
} | ForEach-Object { Write-Host "  EXCLUÍDO: $($_.FullName.Replace($raiz.Path + $sep, ''))" }
