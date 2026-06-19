# Local verification parity with required CI checks (Windows).
# See specs/003-improve-testing-cicd/contracts/local-verification-contract.md
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("--quick", "--full")]
    [string]$Profile
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

function Invoke-Step {
    param(
        [string]$Category,
        [scriptblock]$Command
    )
    Write-Host ""
    Write-Host "==> [$Category]"
    & $Command
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Invoke-Step "Format" { dotnet format Autonocraft.slnx --verify-no-changes }
Invoke-Step "Atlas" { dotnet run --project src/Autonocraft.AtlasBuild -- --check }
Invoke-Step "Unit tests" {
    dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Autonocraft.Tests.Unit"
}

if ($Profile -eq "--full") {
    Invoke-Step "Build" { dotnet build Autonocraft.slnx -c Release }
    Invoke-Step "Integration tests" { dotnet run --project src/Autonocraft -c Release -- --test }
}

Write-Host ""
Write-Host "ALL LOCAL VERIFICATION PASSED ($Profile)"
