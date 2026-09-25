@echo off
setlocal

echo Stopping DocGen public tunnel (cloudflared)...
taskkill /IM cloudflared.exe /F >nul 2>&1
echo Done. The backend and web windows keep running.
echo To stop them as well, run stop-app.bat

pause
endlocal
