[CmdletBinding()]
param(
    [switch]$PrepareOnly,
    [ValidateRange(1, 65535)][int]$FrontendPort = 3000,
    [ValidateRange(1, 65535)][int]$ApiPort = 18080,
    [ValidateRange(1, 65535)][int]$ScoringPort = 8000
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$demoRoot = Join-Path $repositoryRoot '.demo'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Install Git first.' }
if (-not $PrepareOnly) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Install and start Docker Desktop first.' }
    & docker info --format '{{.ServerVersion}}' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Docker is not ready. Start Docker Desktop and retry.' }
}

$components = @(
    @{ Name = 'frontend'; Url = 'https://github.com/exsyyy/alfacredit-frontend.git'; Commit = '986fc3cf2b755c7e0abb976454cb9d187c2566be' },
    @{ Name = 'scoring'; Url = 'https://github.com/uandrosova07-arch/scoring.git'; Commit = 'edc7f014cb746d12b9beff495616d9c5c7ac1703' }
)
New-Item -ItemType Directory -Force -Path $demoRoot | Out-Null
foreach ($component in $components) {
    $checkoutPath = Join-Path $demoRoot $component.Name
    if (-not (Test-Path -LiteralPath $checkoutPath)) {
        & git clone --no-checkout $component.Url $checkoutPath
        if ($LASTEXITCODE -ne 0) { throw "Cannot clone $($component.Name). Check GitHub access and your network." }
    } else {
        $changes = & git -C $checkoutPath status --porcelain
        if ($LASTEXITCODE -ne 0) { throw "Not a Git checkout: $checkoutPath" }
        if ($changes) { throw "Uncommitted changes in $checkoutPath. Preserve them before preparing the demo." }
    }
    & git -C $checkoutPath cat-file -e "$($component.Commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        & git -C $checkoutPath fetch origin
        if ($LASTEXITCODE -ne 0) { throw "Cannot fetch $($component.Name)." }
    }
    & git -C $checkoutPath checkout --detach $component.Commit
    if ($LASTEXITCODE -ne 0) { throw "Cannot select the pinned version of $($component.Name)." }
}
if ($PrepareOnly) { Write-Output 'Sources ready in .demo/frontend and .demo/scoring.'; return }

$portVariables = @{
    DEMO_FRONTEND_PORT = "$FrontendPort"
    DEMO_API_PORT = "$ApiPort"
    DEMO_SCORING_PORT = "$ScoringPort"
}
$previousValues = @{}
foreach ($key in $portVariables.Keys) {
    $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    [Environment]::SetEnvironmentVariable($key, $portVariables[$key], 'Process')
}
try {
    & docker compose -f (Join-Path $repositoryRoot 'compose.demo.yaml') up -d --build --wait --wait-timeout 300
    if ($LASTEXITCODE -ne 0) { throw 'Demo startup failed. Inspect docker compose -f compose.demo.yaml logs --tail 100.' }
    Write-Output "Cabinet: http://localhost:$FrontendPort/"
    Write-Output "Swagger: http://localhost:$ApiPort/swagger"
    Write-Output "ML health: http://localhost:$ScoringPort/health"
    Write-Output 'Demo login: client_good / Demo123! (unless SEED_DEMO_PASSWORD was changed in .env).'
    Write-Output 'Stop without deleting data: docker compose -f compose.demo.yaml stop'
} finally {
    foreach ($key in $portVariables.Keys) {
        [Environment]::SetEnvironmentVariable($key, $previousValues[$key], 'Process')
    }
}
