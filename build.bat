@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "ARCANA_BUILD_RESULT=1"
set "ARCANA_PROJECT_OPEN="
pushd "%~dp0"
if errorlevel 1 (
    >&2 echo ERROR: Could not open the Arcana project directory.
    goto :finish
)
set "ARCANA_PROJECT_OPEN=1"

rem Set SIMS3_GAME_DIR to the base game's folder for a custom install.

if not exist "tools\build_journal.py" goto :missing_builder
if not exist "tools\prepare_sims3_deps.py" goto :missing_builder
if not exist "tools\dbpf.py" goto :missing_builder
if not exist "tools\vampire_bitemark.py" goto :missing_builder
if not exist "tools\check_game_api.py" goto :missing_builder

where py >nul 2>&1
if not errorlevel 1 (
    py -3 -c "import sys; sys.exit(0 if sys.version_info >= (3, 9) else 1)" >nul 2>&1
    if not errorlevel 1 (
        set "ARCANA_PYTHON=py -3"
        goto :build
    )
)
where python >nul 2>&1
if errorlevel 1 goto :missing_python
python -c "import sys; sys.exit(0 if sys.version_info >= (3, 9) else 1)" >nul 2>&1
if errorlevel 1 goto :missing_python
set "ARCANA_PYTHON=python"

:build
echo.
echo Building Arcana...
%ARCANA_PYTHON% -B "tools\build_journal.py" %*
set "ARCANA_BUILD_RESULT=%errorlevel%"
goto :finish

:missing_builder
>&2 echo ERROR: A required build tool is missing from the project.
>&2 echo Extract the complete Arcana source ZIP, including the tools folder.
goto :finish

:missing_python
>&2 echo ERROR: Python 3.9 or newer was not found.
>&2 echo Install Python 3 and enable the Python launcher or add python to PATH.

:finish
echo.
echo ========================================
if "%ARCANA_BUILD_RESULT%"=="0" (
    echo    ARCANA BUILD SUCCESSFUL
) else (
    echo    ARCANA BUILD FAILED
    echo    Exit Code: %ARCANA_BUILD_RESULT%
)
echo ========================================
echo.
echo Press any key to close the build window.
pause >nul
if defined ARCANA_PROJECT_OPEN popd
exit /b %ARCANA_BUILD_RESULT%
