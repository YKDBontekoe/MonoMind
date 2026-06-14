# Cross-platform E2E orchestration for CI (Windows).
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Scripts = Join-Path $Root ".cursor/skills/autonocraft-game-test/scripts"
$Output = Join-Path $Root "test_output"
$Port = if ($env:PORT) { $env:PORT } else { "5001" }

New-Item -ItemType Directory -Force -Path $Output | Out-Null
Set-Location $Root

$GameLog = Join-Path $Output "game.log"
$GameErrLog = Join-Path $Output "game.err.log"
Write-Host "==> Starting game on port $Port..."

$GameArgs = @(
    "exec",
    "src/Autonocraft/bin/Release/net10.0/Autonocraft.dll",
    "--",
    "--skip-menu",
    "--agent-port", $Port
)

$GameProcess = Start-Process -FilePath "dotnet" -ArgumentList $GameArgs `
    -RedirectStandardOutput $GameLog -RedirectStandardError $GameErrLog `
    -PassThru -NoNewWindow

Write-Host "    PID=$($GameProcess.Id) log=$GameLog"

try {
    Write-Host "==> Running live API tests..."
    python "$Scripts/test_live_api.py" --port $Port --wait 180 --output-dir (Join-Path $Output "live_api")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "==> Running JSON scenarios..."
    python "$Scripts/run_scenario.py" --all --port $Port --output-dir (Join-Path $Output "scenarios")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "ALL E2E TESTS PASSED"
}
finally {
    Write-Host "==> Shutting down game (pid $($GameProcess.Id))..."
    if (-not $GameProcess.HasExited) {
        Stop-Process -Id $GameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
