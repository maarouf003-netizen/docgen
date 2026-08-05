@echo off
setlocal

echo Stopping DocGen processes (ports 5199 and 5173)...

for /f "tokens=5" %%a in ('netstat -ano ^| findstr /r /c:":5199 " /c:":5173 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /F >nul 2>&1
)

echo Done.
pause
endlocal
