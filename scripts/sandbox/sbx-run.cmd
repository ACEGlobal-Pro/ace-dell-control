@echo off
echo started %DATE% %TIME% > C:\dc\build\out\SANDBOX-STARTED
powershell -NoProfile -ExecutionPolicy Bypass -File C:\dc\scripts\smoke-test.ps1 > C:\dc\build\out\sandbox-console.log 2>&1
echo exit %ERRORLEVEL% > C:\dc\build\out\SANDBOX-DONE
