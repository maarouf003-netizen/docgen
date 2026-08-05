@echo off
setlocal
set "PATH=C:\Program Files\dotnet;C:\Program Files\nodejs;%PATH%"
set "ROOT=%~dp0"

echo ==========================================================
echo    DocGen - Start (Backend + Frontend + Browser)
echo    Close this window to finish.
echo ==========================================================

if not exist "%ROOT%backend\DocGenerator.sln" (
    echo ERROR: backend project not found. Run this file from react-dotnet-app.
    pause
    exit /b 1
)

echo [1/3] Starting backend (API) on http://localhost:5199 ...
start "DocGen API" /D "%ROOT%backend" cmd /k "dotnet run --project src/DocGenerator.Api --urls http://localhost:5199"

echo [2/3] Starting frontend (Vite) on http://localhost:5173 ...
start "DocGen Frontend" /D "%ROOT%frontend" cmd /k "npm run dev"

echo [3/3] Waiting for services to boot, then opening the browser...
timeout /t 15 /nobreak >nul
start "" "http://localhost:5173"

echo.
echo Done! Keep the two new windows open.
echo To stop everything, double-click stop-app.bat
echo.
pause >nul
endlocal
