@if not defined _ECHO @echo off

set REPO_ROOT=%~dp0

msbuild %REPO_ROOT%\CodexTestRepo\CodexTestRepo.sln /bl:%REPO_ROOT%\CodexTestRepo.binlog /t:Rebuild

echo ""
echo "TEST1"
echo ""

%REPO_ROOT%\..\..\out\bin\Debug\Codex\Codex.exe index -save %REPO_ROOT%\outputs\test1 -p %REPO_ROOT%\CodexTestRepo -repoUrl https://github.com/Ref12/Codex/tree/feature/commitModelView/ -n CodexTestRepo -test -clean -bld %REPO_ROOT%\CodexTestRepo.binlog

echo ""
echo "OPT1"
echo ""

%REPO_ROOT%\..\..\out\bin\Debug\Codex\Codex.exe load -d %REPO_ROOT%\outputs\test1 -save %REPO_ROOT%\outputs\opt1 -clean

echo ""
echo "TEST2"
echo ""

%REPO_ROOT%\..\..\out\bin\Debug\Codex\Codex.exe load -d %REPO_ROOT%\outputs\opt1 -save %REPO_ROOT%\outputs\test2 -test -clean

echo ""
echo "OPT2"
echo ""

%REPO_ROOT%\..\..\out\bin\Debug\Codex\Codex.exe load -d %REPO_ROOT%\outputs\test2 -save %REPO_ROOT%\outputs\opt2 -clean

endlocal