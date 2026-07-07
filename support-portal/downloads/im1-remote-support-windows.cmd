@echo off
setlocal

title iM1 Remote Support Setup
echo.
echo iM1 Remote Support
echo Preparing the Remote Support application...
echo.

set "SCRIPT=%TEMP%\im1-remote-support-setup.ps1"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://support.im1os.com/download/windows-script' -OutFile '%SCRIPT%' -UseBasicParsing } catch { Write-Error $_; exit 1 }"
if errorlevel 1 (
  echo.
  echo Unable to download the setup helper.
  echo Please contact your iM1 technician.
  pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
if errorlevel 1 (
  echo.
  echo Setup could not complete.
  echo Please contact your iM1 technician.
  pause
  exit /b 1
)

echo.
echo Remote Support is ready. Provide your Support ID to your iM1 technician.
timeout /t 5 >nul
exit /b 0
