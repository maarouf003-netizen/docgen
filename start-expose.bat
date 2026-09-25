@echo off
setlocal
set "PATH=C:\Program Files\dotnet;C:\Program Files\nodejs;%PATH%"

echo ==========================================================
echo    DocGen - Start with public link (API + Web + Tunnel)
echo ==========================================================

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-expose.ps1" %*

echo.
pause
endlocal
