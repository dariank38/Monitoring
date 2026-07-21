# Build admin panel and start server
# Usage: .\serve.ps1

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$adminDir = Join-Path $root "Admin"
$serverDir = Join-Path $root "Server"

# Step 1: Install admin dependencies if needed
if (-not (Test-Path (Join-Path $adminDir "node_modules"))) {
    Write-Host "[1/3] Installing admin dependencies..." -ForegroundColor Cyan
    Push-Location $adminDir
    npm install
    Pop-Location
} else {
    Write-Host "[1/3] Admin dependencies already installed" -ForegroundColor Green
}

# Step 2: Build admin panel
Write-Host "[2/3] Building admin panel..." -ForegroundColor Cyan
Push-Location $adminDir
npm run build
Pop-Location
Write-Host "[2/3] Admin panel built to Admin/build/" -ForegroundColor Green

# Step 3: Install server dependencies if needed
if (-not (Test-Path (Join-Path $serverDir "node_modules"))) {
    Write-Host "[3/3] Installing server dependencies..." -ForegroundColor Cyan
    Push-Location $serverDir
    npm install
    Pop-Location
} else {
    Write-Host "[3/3] Server dependencies already installed" -ForegroundColor Green
}

# Start server
Write-Host ""
Write-Host "Starting server..." -ForegroundColor Yellow
Push-Location $serverDir
npm start
Pop-Location
