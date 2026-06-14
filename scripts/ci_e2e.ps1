# Cross-platform E2E orchestration for CI (Windows).
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Scripts = Join-Path $Root ".cursor/skills/autonocraft-game-test/scripts"
$Output = Join-Path $Root "test_output"
$Port = if ($env:PORT) { $env:PORT } else { "5001" }
$WaitSeconds = if ($env:E2E_WAIT_SECONDS) { [int]$env:E2E_WAIT_SECONDS } else { 300 }
$RenderDistance = if ($env:E2E_RENDER_DISTANCE) { $env:E2E_RENDER_DISTANCE } else { "4" }

New-Item -ItemType Directory -Force -Path $Output | Out-Null
Set-Location $Root

$GameLog = Join-Path $Output "game.log"
$GameErrLog = Join-Path $Output "game.err.log"
$DllPath = Join-Path $Root "src/Autonocraft/bin/Release/net10.0/Autonocraft.dll"

Write-Host "==> Starting game on port $Port (render distance $RenderDistance)..."

$GameArgs = @(
    "exec",
    $DllPath,
    "--",
    "--skip-menu",
    "--agent-port", $Port,
    "--render-distance", $RenderDistance
)

$GameProcess = Start-Process -FilePath "dotnet" -ArgumentList $GameArgs `
    -WorkingDirectory $Root `
    -RedirectStandardOutput $GameLog `
    -RedirectStandardError $GameErrLog `
    -PassThru -WindowStyle Hidden

Write-Host "    PID=$($GameProcess.Id) log=$GameLog"

function Show-GameLogs {
    if (Test-Path $GameLog) {
        Write-Host "--- game.log ---"
        Get-Content $GameLog -ErrorAction SilentlyContinue
    }
    if (Test-Path $GameErrLog) {
        Write-Host "--- game.err.log ---"
        Get-Content $GameErrLog -ErrorAction SilentlyContinue
    }
}

Start-Sleep -Seconds 5
if ($GameProcess.HasExited) {
    Write-Host "Game process exited early with code $($GameProcess.ExitCode)"
    Show-GameLogs
    exit 1
}

try {
    Write-Host "==> Running live API tests..."
    python "$Scripts/test_live_api.py" --port $Port --wait $WaitSeconds --output-dir (Join-Path $Output "live_api")
    if ($LASTEXITCODE -ne 0) {
        Show-GameLogs
        exit $LASTEXITCODE
    }

    Write-Host "==> Running JSON scenarios..."
    python "$Scripts/run_scenario.py" --all --port $Port --output-dir (Join-Path $Output "scenarios")
    if ($LASTEXITCODE -ne 0) {
        Show-GameLogs
        exit $LASTEXITCODE
    }

    Write-Host "ALL E2E TESTS PASSED"
}
finally {
    Write-Host "==> Shutting down game (pid $($GameProcess.Id))..."
    if (-not $GameProcess.HasExited) {
        Stop-Process -Id $GameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
