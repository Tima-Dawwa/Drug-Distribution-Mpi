@echo off
cd /d %~dp0

IF NOT EXIST process_count.txt (
    echo The file process_count.txt does not exist. Please run the program first.
    pause
    exit /b
)

set /p totalRanks=<process_count.txt

set exePath=..\bin\Debug\Drug Distribution Mpi Project.exe

IF NOT EXIST "%exePath%" (
    echo Executable does not exist at: %exePath%
    pause
    exit /b
)

echo Running: mpiexec -n %totalRanks% "%exePath%"
echo.

mpiexec -n %totalRanks% "%exePath%"

echo.
echo Done.
pause
