@echo off
powershell.exe -NoProfile -File "%~dp0Install-Portal.ps1"
if errorlevel 1 (
  echo A instalacao nao foi concluida. Leia o erro acima.
  pause
  exit /b 1
)
pause
