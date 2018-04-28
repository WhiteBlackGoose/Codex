@if not defined _ECHO @echo off
setlocal enabledelayedexpansion

set REPO_ROOT=%~dp0

%REPO_ROOT%\out\bin\Debug\Codex\Codex.exe index -save %REPO_ROOT%\out\store\Codex -p %REPO_ROOT% -repoUrl https://github.com/Ref12/Codex/tree/feature/commitModelView/ -n Codex /bld:%REPO_ROOT%\out -noMsBuild

endlocal