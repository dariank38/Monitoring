@echo off
setlocal enabledelayedexpansion

REM Build admin panel and start server
REM Usage: serve.bat

REM Check Node.js is installed and version >= 22
where node >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Node.js is not installed or not in PATH. Install Node.js v22+ from https://nodejs.org
    exit /b 1
)
for /f "tokens=1 delims=." %%v in ('node -v 2^>nul') do set NODE_MAJOR=%%v
set NODE_MAJOR=%NODE_MAJOR:v=%
if %NODE_MAJOR% lss 22 (
    echo ERROR: Node.js v22+ required. Found version %NODE_MAJOR%.
    exit /b 1
)
echo Node.js detected: 
node -v

set ROOT=%~dp0
set ADMIN_DIR=%ROOT%Admin
set SERVER_DIR=%ROOT%Server

REM Step 1: Install admin dependencies if needed
if not exist "%ADMIN_DIR%\node_modules" (
    echo [1/3] Installing admin dependencies...
    pushd "%ADMIN_DIR%"
    call npm install
    popd
) else (
    echo [1/3] Admin dependencies already installed
)

REM Step 2: Build admin panel
echo [2/3] Building admin panel...
pushd "%ADMIN_DIR%"
call npm run build
popd
echo [2/3] Admin panel built to Admin\build\

REM Step 3: Install server dependencies if needed
if not exist "%SERVER_DIR%\node_modules" (
    echo [3/3] Installing server dependencies...
    pushd "%SERVER_DIR%"
    call npm install
    popd
) else (
    echo [3/3] Server dependencies already installed
)

REM Start server
echo.
echo Starting server...
pushd "%SERVER_DIR%"
call npm start
popd
