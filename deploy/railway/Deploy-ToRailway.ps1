<#
.SYNOPSIS
Despliega la rama main actual a Railway production. Sin parametros.

.DESCRIPTION
Convencion del repo: todo el desarrollo vive en main. Esta sesion empuja
main al branch tecnico feat/railway-deploy (que Railway watch-ea) usando
--force-with-lease, y aplica migraciones nuevas si las hay.

Pipeline:
  1. Pre-checks: branch == main, working tree limpio (o solo .gitignore), dotnet, secrets.
  2. Mata procesos locales Propia.Api / Propia.Web que bloqueen las DLLs del build.
  3. git fetch + pull origin main (asegura que tienes lo ultimo del equipo).
  4. dotnet build Release + dotnet test (cubre el repo entero).
  5. git push origin main:feat/railway-deploy --force-with-lease.
     Railway detecta el push y redeploya propia-api + propia-web.
  6. Detecta y aplica migraciones EF nuevas via efbundle.
     - Compara count(local) vs count(Railway PG via __EFMigrationsHistory).
     - Si hay nuevas: genera bundle y aplica. Usa .railway-ddl-url.local si existe,
       o pide DATABASE_PUBLIC_URL del clipboard.
  7. Espera 90s + smoke test contra /health y /health/ready de ambos servicios.

Convencion archivos locales (gitignored):
  .railway-secrets.local   - PROPIA_APP_PWD, JWT_SIGNING_KEY (setup inicial)
  .railway-ddl-url.local   - opcional: DATABASE_PUBLIC_URL guardada

.PARAMETER SkipTests
Salta dotnet test. NO recomendado.

.PARAMETER SkipMigrations
Salta la fase de aplicar migraciones nuevas.

.PARAMETER SkipPull
Salta git pull (util si ya tienes commits locales por empujar).

.PARAMETER ApiUrl
URL publica del servicio API en Railway.

.PARAMETER WebUrl
URL publica del servicio Web en Railway.

.PARAMETER DryRun
Hace todo el flow excepto push y migraciones.

.EXAMPLE
.\Deploy-ToRailway.ps1

.EXAMPLE
.\Deploy-ToRailway.ps1 -DryRun

.EXAMPLE
# Si ya aplicaste migraciones a mano:
.\Deploy-ToRailway.ps1 -SkipMigrations
#>
param(
  [switch]$SkipTests,
  [switch]$SkipMigrations,
  [switch]$SkipPull,
  [string]$ApiUrl = "https://propia-production-e484.up.railway.app",
  [string]$WebUrl = "https://refreshing-laughter-production-d4ec.up.railway.app",
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
# git y dotnet escriben progreso a stderr aun en exito. Sin esto, $ErrorActionPreference="Stop"
# convierte esos mensajes en excepciones terminating y aborta el script. PowerShell 7.4+ permite
# desacoplar native commands: $LASTEXITCODE manda, no el stderr.
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$SourceBranch = "main"
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

function Invoke-Native {
  # Wrapper para ejecutar comandos nativos (git, dotnet) sin que stderr de
  # progreso aborte el script en PowerShell 5.1 (que NO tiene
  # $PSNativeCommandUseErrorActionPreference). Bajamos temporal a Continue
  # alrededor de la llamada y verificamos solo $LASTEXITCODE.
  param([string]$Cmd, [string[]]$Args, [string]$Label = $null)
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  try {
    $output = (& $Cmd @Args 2>&1 | Out-String).Trim()
    $exit = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $prev
  }
  if ($exit -ne 0) {
    $what = if ($Label) { $Label } else { "$Cmd $($Args -join ' ')" }
    Fail "${what} fallo (exit $exit):`n$output"
  }
  return $output
}

function Invoke-Git {
  # Sugar para git: Invoke-Git "push" "origin" "main"
  return Invoke-Native -Cmd "git" -Args $args
}

# ============================================================================
# Step 1: Pre-checks
# ============================================================================
Step "1/7 Pre-checks"

$currentBranch = (git branch --show-current).Trim()
if ($currentBranch -ne $SourceBranch) {
  Fail "Estas en '$currentBranch'. Cambia a $SourceBranch primero: git checkout $SourceBranch"
}
Ok "En $SourceBranch"

$status = git status --porcelain
$dirtyExceptGitignore = $status | Where-Object {
  $_ -notmatch "^\s*M\s+\.gitignore\s*$" -and $_ -notmatch "^\?\?\s+"
}
if ($dirtyExceptGitignore) {
  Fail "Working tree con cambios sin commitear:`n$status"
}
$untracked = $status | Where-Object { $_ -match "^\?\?\s+" }
if ($untracked) {
  Info "Archivos sin trackear (ignorados por el deploy):"
  $untracked | ForEach-Object { Info "    $_" }
}
Ok "Working tree limpio"

$SecretsFile = Join-Path $RepoRoot ".railway-secrets.local"
if (-not (Test-Path $SecretsFile)) {
  Fail "Falta $SecretsFile con PROPIA_APP_PWD. Setup inicial pendiente."
}
Ok "Secrets disponibles"

$dotnetVersion = (dotnet --version 2>&1)
if ($LASTEXITCODE -ne 0) { Fail "dotnet no encontrado en PATH" }
Ok "dotnet $dotnetVersion"

# ============================================================================
# Step 2: Matar procesos Propia locales
# ============================================================================
Step "2/7 Limpiar procesos locales que bloquean DLLs"

$blockers = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "Propia*" }
if ($blockers) {
  $blockers | ForEach-Object {
    Info "Matando $($_.Name) (PID $($_.Id))"
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
  }
  Start-Sleep -Seconds 2
}

$repoPathEscaped = $RepoRoot.Path -replace '\\', '\\\\'
$running = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -like "*$($RepoRoot.Path -replace '\\', '\\')*" }
if ($running) {
  $running | ForEach-Object {
    Info "Matando dotnet run (PID $($_.ProcessId))"
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
  }
  Start-Sleep -Seconds 2
}
Ok "Procesos limpiados"

# ============================================================================
# Step 3: Pull main del remoto (sync con equipo)
# ============================================================================
Step "3/7 Pull origin main"

if ($SkipPull) {
  Info "(skip por -SkipPull)"
} else {
  Invoke-Git "fetch" "origin" | Out-Null
  $behindAhead = (git rev-list --left-right --count "main...origin/main").Split()
  $behind = [int]$behindAhead[1]
  $ahead = [int]$behindAhead[0]
  if ($behind -gt 0) {
    Info "main esta $behind commits detras del remoto. Pulling..."
    Invoke-Git "pull" "--ff-only" "origin" "main" | Out-Null
  }
  if ($ahead -gt 0) {
    Info "main local tiene $ahead commits que NO estan en remoto. Se empujaran ahora."
  }
  Ok "Sincronizado con origin/main"
}

# ============================================================================
# Step 4: Build + tests
# ============================================================================
Step "4/7 Build Release + Tests"

Info "dotnet build (Release)..."
$buildLog = Invoke-Native -Cmd "dotnet" -Args @("build", "--configuration", "Release", "-nologo", "-clp:NoSummary") -Label "dotnet build"
Ok "Build verde"

if (-not $SkipTests) {
  Info "dotnet test... (~2 min con Testcontainers)"
  $testLog = Invoke-Native -Cmd "dotnet" -Args @("test", "--no-build", "--configuration", "Release", "--logger", "console;verbosity=minimal", "-nologo") -Label "dotnet test"
  $passedLine = ($testLog -split "`n") | Select-String -Pattern "Superado:\s+\d+,\s+Total:\s+\d+" | Select-Object -Last 1
  if ($passedLine) { Ok "Tests verde - $($passedLine.Matches[0].Value)" } else { Ok "Tests verde" }
} else {
  Info "(saltando tests por -SkipTests)"
}

# ============================================================================
# Step 5: Push main -> feat/railway-deploy
# ============================================================================
Step "5/7 Push main -> $TargetBranch (Railway redeploya auto)"

# Sync main local a remoto primero
$ahead = [int](git rev-list --left-right --count "main...origin/main").Split()[0]
if ($ahead -gt 0) {
  if ($DryRun) {
    Info "(DryRun) saltaria git push origin main"
  } else {
    Invoke-Git "push" "origin" "main" | Out-Null
    Ok "main sincronizado con remoto"
  }
}

if ($DryRun) {
  Info "(DryRun) saltaria git push origin main:$TargetBranch --force-with-lease"
  Ok "Skip push"
} else {
  $pushOutput = Invoke-Git "push" "origin" "main:$TargetBranch" "--force-with-lease"
  if ($pushOutput) { Write-Host $pushOutput -ForegroundColor Gray }
  Ok "Push hecho. Railway empezo redeploy automatico."
}

# ============================================================================
# Step 6: Migraciones nuevas (compara local vs Railway PG)
# ============================================================================
Step "6/7 Aplicar migraciones nuevas a Railway PG"

if ($SkipMigrations -or $DryRun) {
  Info "(saltando por -SkipMigrations o -DryRun)"
} else {
  $localMigrations = (Get-ChildItem -Path "src/Propia.Infrastructure/Persistence/Migrations" -Filter "*.cs" |
    Where-Object { $_.Name -notlike "*Designer*" -and $_.Name -notlike "*Snapshot*" }).Count
  Info "Migraciones locales: $localMigrations"

  # Cargar DDL URL
  $DdlUrlFile = Join-Path $RepoRoot ".railway-ddl-url.local"
  $ddlUrl = $null
  if (Test-Path $DdlUrlFile) {
    $ddlUrl = (Get-Content $DdlUrlFile -Raw).Trim()
    Info "Usando DDL URL de .railway-ddl-url.local"
  } else {
    Info "Pegate la DATABASE_PUBLIC_URL desde Railway -> Postgres -> Variables (icono copy)."
    Info "Leo el clipboard en 5 segundos..."
    Start-Sleep -Seconds 5
    $ddlUrl = (Get-Clipboard).Trim() -replace "`r|`n", ""
  }

  if (-not ($ddlUrl -match "^postgresql://postgres:")) {
    Info "DDL URL no valida. Saltando migraciones."
    Info "Aplicalas manual: dotnet ef migrations bundle ... && ./efbundle.exe --connection ..."
  } else {
    # Parsear a Npgsql keyword=value
    $u = $ddlUrl -replace 'postgresql://([^:]+):.*', '$1'
    $p = $ddlUrl -replace 'postgresql://[^:]+:([^@]+)@.*', '$1'
    $h = $ddlUrl -replace 'postgresql://[^:]+:[^@]+@([^:]+):.*', '$1'
    $port = $ddlUrl -replace '.*:(\d+)/.*', '$1'
    $db = $ddlUrl -replace '.*/([^/]+)$', '$1'
    $npgsql = "Host=$h;Port=$port;Database=$db;Username=$u;Password=$p;SSL Mode=Require;Trust Server Certificate=true"

    Info "Generando efbundle..."
    Invoke-Native -Cmd "dotnet" -Args @(
      "ef", "migrations", "bundle",
      "--project", "src/Propia.Infrastructure",
      "--startup-project", "src/Propia.Api",
      "--output", "./efbundle.exe",
      "--self-contained",
      "--target-runtime", "win-x64",
      "--configuration", "Release"
    ) -Label "dotnet ef migrations bundle" | Out-Null

    Info "Aplicando contra $h..."
    $bundleOutput = Invoke-Native -Cmd "./efbundle.exe" -Args @("--connection", $npgsql) -Label "efbundle apply"
    $applied = ($bundleOutput -split "`n") | Select-String -Pattern "Applying migration"
    Remove-Item ./efbundle.exe -ErrorAction SilentlyContinue
    if ($applied) {
      Ok "Migraciones aplicadas: $($applied.Count)"
      $applied | ForEach-Object { Info "    $($_.Line.Trim())" }
    } else {
      Ok "Sin migraciones nuevas que aplicar"
    }
  }
}

# ============================================================================
# Step 7: Smoke test
# ============================================================================
Step "7/7 Smoke test (esperando 90s a que Railway termine el redeploy)"

if ($DryRun) {
  Info "(DryRun - skip smoke)"
} else {
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
      if ($resp.StatusCode -eq 200) { Ok "$($e.Name) - 200" }
      else { Info "$($e.Name) - HTTP $($resp.StatusCode)"; $allOk = $false }
    } catch {
      Info "$($e.Name) - ERROR: $($_.Exception.Message)"
      $allOk = $false
    }
  }

  if ($allOk) {
    Write-Host "`nDEPLOY EXITOSO - Sistema operativo en Railway." -ForegroundColor Green
    Write-Host "  Web:  $WebUrl" -ForegroundColor Gray
    Write-Host "  API:  $ApiUrl" -ForegroundColor Gray
  } else {
    Write-Host "`nDEPLOY CON ADVERTENCIAS - revisar Railway dashboard." -ForegroundColor Yellow
    Write-Host "  https://railway.com/project/ed01a3b1-eeeb-4026-a456-a14619c7e534" -ForegroundColor Yellow
    exit 3
  }
}

} finally {
  Pop-Location
}
