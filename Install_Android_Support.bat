@echo off
title HeavyVehiclesWorld - Install Android Build Support
echo ==============================================================================
echo [HeavyVehiclesWorld] Unity Android Build Support Installer
echo Proprietary Rights: Shivraj Deshmukh
echo ==============================================================================
echo.
echo Unity Editor requires Administrator permissions to install the Android
echo module into 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1'.
echo.
echo Launching elevated installer... Please click [YES] on the Windows UAC prompt.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -Command \"\"\"Write-Host \"[1/2] Setting up Unity CLI environment...\"; $env:PATH = \"$env:LOCALAPPDATA\Unity\bin;$env:PATH\"; Write-Host \"[2/2] Downloading and installing Android Build Support for Unity 6 (6000.6.9f1)...\"; & unity install-modules --editor-version 6000.6.9f1 -a arm64 -m android --accept-eula; Write-Host \"\"; Write-Host \"=============================================================\" -ForegroundColor Green; Write-Host \"[SUCCESS] Android Build Support installed successfully!\" -ForegroundColor Green; Write-Host \"You can now return to the chat or run Build_Production_APK.bat\" -ForegroundColor Green; Write-Host \"=============================================================\" -ForegroundColor Green; Write-Host \"\"; Write-Host \"Press any key to close this window...\"; $null = $Host.UI.RawUI.ReadKey(\"NoEcho,IncludeKeyDown\")\"\"\"'"
