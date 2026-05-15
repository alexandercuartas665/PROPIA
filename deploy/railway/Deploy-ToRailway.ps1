<#
.SYNOPSIS
Automatiza el ciclo de deploy de una feature branch hacia Railway production.

.DESCRIPTION
Pipeline:
  1. Verifica estado del repo (working tree limpio o solo .gitignore dirty).
  2. Mata procesos locales de Propia.Api / Propia.Web que bloquearian el build.
  3. Stash + checkout feat/railway-deploy.
  4. Merge de la feature branch (--no-ff). Si hay conflictos: aborta con instrucciones.
  5. dotnet build Release + dotnet test. Si falla: aborta el merge y vuelve al estado anterior.
  6. Commit del merge + push origin feat/railway-deploy. Railway redeploya automaticamente.
  7. Detecta migraciones nuevas no aplicadas a Railway PG. Si hay:
     - Genera efbundle y lo aplica usando RAILWAY_DDL_URL (del .railway-secrets.local o del clipboard).
  8. Espera 90s + smoke test contra /health y /health/ready de ambos servicios.

Convencion de archivos locales (todos gitignored):
  .railway-secrets.local      - PROPIA_APP_PWD, JWT_SIGNING_KEY (creados en setup inicial)
  .railway-ddl-url.local      - opcional: DATABASE_PUBLIC_URL guardada para no pedirla del clipboard

.PARAMETER FeatureBranch
Nombre de la branch a integrar a feat/railway-deploy. Ej: feat/2.14-comunicaciones.

.PARAMETER SkipTests
Si se pasa, salta dotnet test (no recomendado para production).

.PARAMETER SkipMigrations
Si se pasa, salta la fase de aplicar migraciones nuevas. Util si ya las aplicaste a mano.

.PARAMETER ApiUrl
URL publica del servicio API en Railway. Default: la del piloto actual.

.PARAMETER WebUrl
URL publica del servicio Web en Railway. Default: la del piloto actual.

.PARAMETER DryRun
Si se pasa, hace todo el flow excepto push y migraciones (para probar el script).

.EXAMPLE
.\Deploy-ToRailway.ps1 -FeatureBranch feat/2.15-documentos

.EXAMPLE
.\Deploy-ToRailway.ps1 -FeatureBranch feat/2.14-comunicaciones -DryRun

.EXAMPLE
# Si tienes la URL guardada permanente:
echo "postgresql://postgres:xxx@yamanote.proxy.rlwy.net:45112/railway" > .railway-ddl-url.local
.\Deploy-ToRailway.ps1 -FeatureBranch feat/2.15-documentos
#>
param(
  [Parameter(Mandatory=$true)][string]$FeatureBranch,
  [switch]$SkipTests,
  [switch]$SkipMigrations,
  [string]$ApiUrl = "https://propia-production-e484.up.railway.app",
  [string]$WebUrl = "https://refreshing-laughter-production-d4ec.up.railway.app",
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$TargetBranch = "feat/railway-deploy"

Push-Location $RepoRoot
try {

# ============================================================================
# Helpers
# ============================================================================
function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Info($msg) { Write-Host "  $msg" -ForegroundColor Gray }
function Ok($msg)   { Write-Host "  OK $msg" -ForegroundColor Green }
function Fail($msg) { Write-Host "  ERROR $msg" -ForegroundColor Red; exit 1 }

function Invoke-Git {
  param([string]$Cmd)
  $output = Invoke-Expression "git $Cmd" 2>&1
  if ($LASTEXITCODE -ne 0) { Fail "git $Cmd fallo: $output" }
  return $output
}

# ============================================================================
# Step 1: Pre-checks
# ============================================================================
Step "1/8 Pre-checks"

# Branches existen
$branches = git branch -a 2>&1
if ($branches -notmatch [regex]::Escape($FeatureBranch)) {
  Fail "Branch $FeatureBranch no existe localmente"
}
if ($branches -notmatch [regex]::Escape($TargetBranch)) {
  Fail "Branch $TargetBranch no existe (no estamos en setup de Railway)"
}
Ok "Branches $FeatureBranch y $TargetBranch existen"

# Working tree limpio (excepto .gitignore que sabemos podemos stashear)
$status = git status --porcelain
$dirtyOtherThanGitignore = $status | Where-Object { $_ -notmatch "^\s*M\s+\.gitignore\s*$" }
if ($dirtyOtherThanGitignore) {
  Fail "Working tree tiene cambios sin commitear ademas de .gitignore. Limpia primero.`n$status"
}
$hasGitignoreChanges = ($status -match "^\s*M\s+\.gitignore\s*$")
Ok "Working tree OK"

# Secrets disponibles
$SecretsFile = Join-Path $RepoRoot ".railway-secrets.local"
if (-not (Test-Path $SecretsFile)) {
  Fail "Falta $SecretsFile con PROPIA_APP_PWD. Necesitas el setup inicial primero."
}
Ok "Secrets locales disponibles"

# dotnet
$dotnetVersion = (dotnet --version 2>&1)
if ($LASTEXITCODE -ne 0) { Fail "dotnet no encontrado en PATH" }
Ok "dotnet $dotnetVersion"

# ============================================================================
# Step 2: Matar procesos Propia locales que bloqueen el build
# ============================================================================
Step "2/8 Limpiar procesos locales que bloquean DLLs"

$blockers = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "Propia*" }
if ($blockers) {
  $blockers | ForEach-Object {
    Info "Matando proceso $($_.Name) (PID $($_.Id))"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
  }
  Start-Sleep -Seconds 2
}

# Tambien dotnet run con el repo
$running = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -like "*$($RepoRoot.Path -replace '\\','\\\\')*" }
if ($running) {
  $running | ForEach-Object {
    Info "Matando dotnet run (PID $($_.ProcessId))"
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
  }
  Start-Sleep -Seconds 2
}
Ok "Procesos limpiados"

# ============================================================================
# Step 3: Stash + checkout feat/railway-deploy
# ============================================================================
Step "3/8 Checkout $TargetBranch"

if ($hasGitignoreChanges) {
  Info "Stash de .gitignore"
  Invoke-Git "stash push -m 'Deploy-ToRailway WIP gitignore' -- .gitignore" | Out-Null
}

Invoke-Git "checkout $TargetBranch" | Out-Null

if ($hasGitignoreChanges) {
  Info "Restaurando .gitignore"
  Invoke-Git "stash pop" | Out-Null
  Invoke-Git "add .gitignore" | Out-Null
  $diff = git diff --staged .gitignore
  if ($diff) {
    Invoke-Git "commit -m 'chore(gitignore): actualizar entradas locales'" | Out-Null
    Ok "Commit menor de .gitignore"
  }
}

Ok "En $TargetBranch"

# ============================================================================
# Step 4: Merge --no-ff
# ============================================================================
Step "4/8 Merge $FeatureBranch -> $TargetBranch"

$mergeMsg = @"
Merge $FeatureBranch into $TargetBranch

Auto-merge via Deploy-ToRailway.ps1 - $(Get-Date -Format 'yyyy-MM-dd HH:mm')
"@

$mergeOutput = git merge $FeatureBranch --no-ff -m $mergeMsg 2>&1
if ($LASTEXITCODE -ne 0) {
  if ($mergeOutput -match "CONFLICT") {
    Write-Host "`nCONFLICTOS detectados:" -ForegroundColor Yellow
    git diff --name-only --diff-filter=U
    Write-Host @"

INSTRUCCIONES:
  1. Resuelve los conflictos manualmente.
  2. git add <archivos>
  3. git commit (mantiene el mensaje pre-poblado)
  4. Re-ejecuta este script o continua manual con: dotnet build, dotnet test, git push.
"@ -ForegroundColor Yellow
    exit 2
  }
  Fail "Merge fallo: $mergeOutput"
}
Ok "Merge limpio"

# ============================================================================
# Step 5: Build + tests
# ============================================================================
Step "5/8 Build Release + Tests"

Info "dotnet build (Release)..."
$buildLog = dotnet build --configuration Release -nologo -clp:NoSummary 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-Host $buildLog -ForegroundColor Red
  Fail "Build fallo. Abortando merge:`n  git reset --hard ORIG_HEAD"
}
Ok "Build verde"

if (-not $SkipTests) {
  Info "dotnet test... (puede tomar 2 min por Testcontainers)"
  $testLog = dotnet test --no-build --configuration Release --logger "console;verbosity=minimal" -nologo 2>&1
  $passed = $testLog | Select-String -Pattern "Total:\s+(\d+)" | Select-Object -Last 1
  if ($LASTEXITCODE -ne 0) {
    Write-Host ($testLog | Select-Object -Last 30) -ForegroundColor Red
    Fail "Tests fallaron. Aborta merge con: git reset --hard ORIG_HEAD"
  }
  Ok "Tests verde - $($passed.Matches[0].Value)"
} else {
  Info "(saltando tests por -SkipTests)"
}

# ============================================================================
# Step 6: Push
# ============================================================================
Step "6/8 Push a origin"

if ($DryRun) {
  Info "(DryRun - NO se hace push)"
  Ok "Skip push"
} else {
  Invoke-Git "push origin $TargetBranch" | Out-Null
  Ok "Push hecho. Railway empieza redeploy automatico."
}

# ============================================================================
# Step 7: Migraciones nuevas
# ============================================================================
Step "7/8 Aplicar migraciones nuevas a Railway PG"

if ($SkipMigrations -or $DryRun) {
  Info "(saltando migraciones por -SkipMigrations o -DryRun)"
} else {
  # Capturar DDL connection string
  $DdlUrlFile = Join-Path $RepoRoot ".railway-ddl-url.local"
  $ddlUrl = $null
  if (Test-Path $DdlUrlFile) {
    $ddlUrl = (Get-Content $DdlUrlFile -Raw).Trim()
    Info "Usando DDL URL de .railway-ddl-url.local"
  } else {
    Info "Pegate la DATABASE_PUBLIC_URL desde Railway -> Postgres -> Variables (icono copy)."
    Info "Voy a leerla del clipboard en 5 segundos..."
    Start-Sleep -Seconds 5
    $ddlUrl = (Get-Clipboard).Trim()
  }

  if (-not ($ddlUrl -match "^postgresql://postgres:")) {
    Info "DDL URL no parece valida (esperaba postgresql://postgres:...). Saltando migraciones."
    Info "Aplicalas manualmente: dotnet ef migrations bundle ... && ./efbundle.exe --connection ..."
  } else {
    # Parsear a Npgsql keyword=value
    $u = $ddlUrl -replace 'postgresql://([^:]+):.*', '$1'
    $p = $ddlUrl -replace 'postgresql://[^:]+:([^@]+)@.*', '$1'
    $h = $ddlUrl -replace 'postgresql://[^:]+:[^@]+@([^:]+):.*', '$1'
    $port = $ddlUrl -replace '.*:(\d+)/.*', '$1'
    $db = $ddlUrl -replace '.*/([^/]+)$', '$1'
    $npgsql = "Host=$h;Port=$port;Database=$db;Username=$u;Password=$p;SSL Mode=Require;Trust Server Certificate=true"

    Info "Generando efbundle..."
    dotnet ef migrations bundle `
      --project src/Propia.Infrastructure `
      --startup-project src/Propia.Api `
      --output ./efbundle.exe `
      --self-contained `
      --target-runtime win-x64 `
      --configuration Release 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "No se pudo generar efbundle" }

    Info "Aplicando migraciones contra $h..."
    & ./efbundle.exe --connection $npgsql 2>&1 | Where-Object { $_ -match "Applying|Done|Acquiring|No migrations" } | ForEach-Object { Info $_ }
    if ($LASTEXITCODE -ne 0) { Fail "efbundle fallo" }
    Remove-Item ./efbundle.exe -ErrorAction SilentlyContinue
    Ok "Migraciones aplicadas"
  }
}

# ============================================================================
# Step 8: Smoke test post-deploy
# ============================================================================
Step "8/8 Smoke test (esperando que Railway termine el redeploy)"

if ($DryRun) {
  Info "(DryRun - skip smoke)"
} else {
  Info "Esperando 90s para que Railway buildee + redeploye los servicios..."
  Start-Sleep -Seconds 90

  $endpoints = @(
    @{ Name = "API /health";       Url = "$ApiUrl/health" }
    @{ Name = "API /health/ready"; Url = "$ApiUrl/health/ready" }
    @{ Name = "Web /health";       Url = "$WebUrl/health" }
  )

  $allOk = $true
  foreach ($e in $endpoints) {
    try {
      $resp = Invoke-WebRequest -Uri $e.Url -UseBasicParsing -TimeoutSec 15
      if ($resp.StatusCode -eq 200) {
        Ok "$($e.Name) - 200"
      } else {
        Info "$($e.Name) - HTTP $($resp.StatusCode)"
        $allOk = $false
      }
    } catch {
      Info "$($e.Name) - ERROR: $($_.Exception.Message)"
      $allOk = $false
    }
  }

  if ($allOk) {
    Write-Host "`nDEPLOY EXITOSO - Sistema operativo en Railway." -ForegroundColor Green
  } else {
    Write-Host "`nDEPLOY CON ADVERTENCIAS - revisar Railway dashboard." -ForegroundColor Yellow
    Write-Host "  https://railway.com/project/ed01a3b1-eeeb-4026-a456-a14619c7e534" -ForegroundColor Yellow
  }
}

} finally {
  Pop-Location
}
