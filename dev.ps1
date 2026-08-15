<#
.SYNOPSIS
    Hermes All-in-One Development Environment Runner
.DESCRIPTION
    Starts pre-built infrastructure containers in Docker (MySQL, Redis, MailHog) 
    and launches Hermes.Api, Hermes.Worker, and Hermes.WebFrontend with Hot-Reload (dotnet watch).
.PARAMETER Stop
    Stops all background dotnet processes and shuts down Docker infrastructure.
.PARAMETER Restart
    Restarts Docker infrastructure and all Hermes application services.
.PARAMETER Status
    Displays the health status of Docker containers and listening ports.
.PARAMETER NoWatch
    Runs projects with 'dotnet run' instead of 'dotnet watch'.
.PARAMETER MigrateOnly
    Applies EF Core database migrations and exits.
.PARAMETER NoBrowser
    Prevents automatic opening of the web browser.
#>
[CmdletBinding()]
param (
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Status,
    [switch]$NoWatch,
    [switch]$MigrateOnly,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = (Get-Location).Path }
$ComposeFile = Join-Path $ScriptRoot "Docker\docker-compose.infra.yml"

function Show-Header {
    Write-Host ""
    Write-Host " ========================================================" -ForegroundColor Cyan
    Write-Host "   HERMES - ALL-IN-ONE DEV RUNNER (HYBRID SETUP)        " -ForegroundColor White
    Write-Host " ========================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return $true
    } catch {
        return $false
    }
}

function Stop-HermesDev {
    Show-Header
    Write-Host " [1/2] Stopping dotnet watch/run processes..." -ForegroundColor Yellow
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowTitle -like "*Hermes*" -or $_.CommandLine -like "*Hermes.*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue

    Write-Host " [2/2] Stopping Docker infrastructure containers..." -ForegroundColor Yellow
    if (Test-Path $ComposeFile) {
        docker compose -f $ComposeFile down
    }

    Write-Host ""
    Write-Host " [OK] All Hermes development services have been stopped." -ForegroundColor Green
    Write-Host ""
}

function Show-HermesStatus {
    Show-Header
    Write-Host " --- Docker Infrastructure Containers ---" -ForegroundColor Cyan
    if (Test-DockerRunning) {
        docker compose -f $ComposeFile ps
    } else {
        Write-Host " Docker daemon is not running." -ForegroundColor Red
    }

    Write-Host ""
    Write-Host " --- Port Availability Check ---" -ForegroundColor Cyan
    $ports = @(
        @{ Name = "MySQL (hermes-mysql)"; Port = 3308 },
        @{ Name = "Redis (hermes-redis)"; Port = 6379 },
        @{ Name = "MailHog SMTP"; Port = 1025 },
        @{ Name = "MailHog Web UI"; Port = 8025 },
        @{ Name = "Hermes.Api"; Port = 5165 },
        @{ Name = "Hermes.Worker"; Port = 5166 },
        @{ Name = "Hermes.WebFrontend"; Port = 7016 }
    )

    foreach ($p in $ports) {
        $conn = Test-NetConnection -ComputerName 127.0.0.1 -Port $p.Port -WarningAction SilentlyContinue
        $statusStr = if ($conn.TcpTestSucceeded) { "[ONLINE]" } else { "[OFFLINE]" }
        $color = if ($conn.TcpTestSucceeded) { "Green" } else { "Gray" }
        Write-Host ("  {0,-28} : {1,5} {2}" -f $p.Name, $p.Port, $statusStr) -ForegroundColor $color
    }
    Write-Host ""
}

function Invoke-Migrations {
    Write-Host " Applying EF Core database migrations..." -ForegroundColor Cyan
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    dotnet ef database update --project "$ScriptRoot\Hermes.Infrastructure" --startup-project "$ScriptRoot\Hermes.Api" 2>$null
    $efExit = $LASTEXITCODE
    $ErrorActionPreference = $prevEAP
    if ($efExit -ne 0) {
        Write-Host ""
        Write-Host " [ERROR] EF Core migrations failed (exit code $efExit). Check the output above." -ForegroundColor Red
        Write-Host "         Make sure 'dotnet-ef' is installed: dotnet tool install --global dotnet-ef" -ForegroundColor Yellow
        Write-Host ""
    } else {
        Write-Host " [OK] Database schema is up to date." -ForegroundColor Green
    }
}

if ($Stop) {
    Stop-HermesDev
    exit 0
}

if ($Restart) {
    Stop-HermesDev
    Start-Sleep -Seconds 2
}

if ($Status) {
    Show-HermesStatus
    exit 0
}

Show-Header

# 1. Check Docker Daemon
if (-not (Test-DockerRunning)) {
    Write-Host " [ERROR] Docker daemon is not running! Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# 2. Start Docker Infrastructure (Prebuilt images: MySQL, Redis, MailHog)
Write-Host " [1/4] Starting Docker infrastructure (MySQL, Redis, MailHog)..." -ForegroundColor Cyan
docker compose -f $ComposeFile up -d

Write-Host " [2/4] Waiting for MySQL container to be healthy..." -ForegroundColor Cyan
$retries = 15
$mysqlReady = $false
while ($retries -gt 0 -and -not $mysqlReady) {
    $health = (docker inspect --format='{{json .State.Health.Status}}' hermes-mysql 2>$null) -replace '"',''
    if ($health -eq "healthy" -or $health -eq "") {
        $mysqlReady = $true
        break
    }
    Start-Sleep -Seconds 2
    $retries--
}

if ($MigrateOnly) {
    Invoke-Migrations
    exit 0
}

# 3. Database Migration
Invoke-Migrations

# 4. Launch .NET Projects
$commandType = if ($NoWatch) { "run" } else { "watch" }
Write-Host " [3/4] Launching .NET services with 'dotnet $commandType' (Hot-Reload enabled)..." -ForegroundColor Cyan

# Start API
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$ScriptRoot`"; `$Host.UI.RawUI.WindowTitle = 'Hermes.Api (Port 5165)'; dotnet $commandType --project Hermes.Api"

# Start Worker
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$ScriptRoot`"; `$Host.UI.RawUI.WindowTitle = 'Hermes.Worker (Port 5166)'; dotnet $commandType --project Hermes.Worker"

# Start WebFrontend
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$ScriptRoot`"; `$Host.UI.RawUI.WindowTitle = 'Hermes.WebFrontend (Port 7016)'; dotnet $commandType --project Hermes.WebFrontend/Hermes.WebFrontend --launch-profile https"

# 5. Output Service Overview Dashboard
Write-Host ""
Write-Host " [4/4] All Hermes services are launching!" -ForegroundColor Green
Write-Host ""
Write-Host " ========================================================" -ForegroundColor White
Write-Host "   SERVICE OVERVIEW & DASHBOARDS                        " -ForegroundColor White
Write-Host " ========================================================" -ForegroundColor White
Write-Host "   Frontend Web App : https://localhost:7016" -ForegroundColor Green
Write-Host "   Backend API      : http://localhost:5165" -ForegroundColor Cyan
Write-Host "   Worker Hangfire  : http://localhost:5166/hangfire" -ForegroundColor Cyan
Write-Host "   MailHog Inbox    : http://localhost:8025" -ForegroundColor Yellow
Write-Host "   MySQL Database   : localhost:3308 (Database: hermes)" -ForegroundColor Gray
Write-Host "   Redis Cache      : localhost:6379" -ForegroundColor Gray
Write-Host " ========================================================" -ForegroundColor White
Write-Host ""
Write-Host " Tips:" -ForegroundColor Yellow
Write-Host "   - To stop all services: run  ./dev.ps1 -Stop" -ForegroundColor Gray
Write-Host "   - To check status:      run  ./dev.ps1 -Status" -ForegroundColor Gray
Write-Host ""

if (-not $NoBrowser) {
    Start-Sleep -Seconds 3
    Write-Host " Opening browser at https://localhost:7016..." -ForegroundColor Cyan
    Start-Process "https://localhost:7016"
}
