@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM Quick commit script for Unity project
REM - Stages all changes
REM - Commits with provided message or epoch timestamp
REM - Pushes to origin, sets upstream if missing

REM Determine commit message (use all args as message). If empty, fallback to epoch seconds
if "%~1"=="" (
    for /f "usebackq tokens=*" %%i in (`powershell -NoProfile -Command "[int64]((Get-Date).ToUniversalTime()-(Get-Date '1970-01-01')).TotalSeconds"`) do set "TIMESTAMP=%%i"
    set "COMMIT_MSG=!TIMESTAMP!"
) else (
    set "COMMIT_MSG=%*"
)

REM Show current branch
for /f "tokens=*" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "BRANCH=%%b"
if not defined BRANCH (
    echo Git repository not found in current directory. Please run this script from the repo root.
    pause
    exit /b 1
)

echo Staging changes...
git add -A

REM Check if there are staged changes
git diff --cached --quiet
if %errorlevel%==0 (
    echo No changes to commit. Skipping commit and push.
    pause
    exit /b 0
)

echo Creating commit on branch %BRANCH% with message: !COMMIT_MSG!
git commit -m "!COMMIT_MSG!"
if %errorlevel% neq 0 (
    echo Commit failed.
    pause
    exit /b 1
)

REM If upstream is set, pull --rebase to reduce push conflicts
git rev-parse --abbrev-ref --symbolic-full-name @{u} >nul 2>&1
if %errorlevel%==0 (
    echo Rebasing onto upstream...
    git pull --rebase
    if %errorlevel% neq 0 (
        echo Rebase failed. Resolve conflicts and re-run the script.
        pause
        exit /b 1
    )
) else (
    echo No upstream configured for %BRANCH%. Upstream will be set on push.
)

echo Pushing to origin/%BRANCH%...
git push -u origin "%BRANCH%"
if %errorlevel% neq 0 (
    echo Push failed.
    pause
    exit /b 1
)

echo Successfully committed and pushed with message: !COMMIT_MSG!
pause
exit /b 0