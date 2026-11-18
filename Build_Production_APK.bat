@echo off
title HeavyVehiclesWorld - Build Production APK
echo ==============================================================================
echo [HeavyVehiclesWorld] Build Production Release APK (Signed ARM64 IL2CPP)
echo Proprietary Rights: Shivraj Deshmukh
echo ==============================================================================
echo.
echo Starting headless Unity build process...
echo Project: C:\Users\Shivraj Deshmukh\Desktop\Game\HeavyVehiclesWorld
echo Method:  HVW_BatchBuild.BuildProductionAPK
echo Output:  Builds\Android\HeavyVehiclesWorld_Production.apk
echo.

set UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe
set PROJECT_DIR=%~dp0
if "%PROJECT_DIR:~-1%"=="\" set PROJECT_DIR=%PROJECT_DIR:~0,-1%

"%UNITY_EXE%" -batchmode -quit -projectPath "%PROJECT_DIR%" -licensingIpc "LicenseClient-Shivraj Deshmukh" -executeMethod HVW_BatchBuild.BuildProductionAPK -logFile "%PROJECT_DIR%\Logs\apk_build.log"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo =============================================================
    echo [SUCCESS] HeavyVehiclesWorld Production APK Built Successfully!
    echo Location: %PROJECT_DIR%\Builds\Android\HeavyVehiclesWorld_Production.apk
    echo =============================================================
) else (
    echo.
    echo =============================================================
    echo [FAILED] Build exited with code %ERRORLEVEL%.
    echo Please check %PROJECT_DIR%\Logs\apk_build.log for details.
    echo =============================================================
)

echo.
pause
