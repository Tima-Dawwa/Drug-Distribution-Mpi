@echo off
cd /d %~dp0

echo.
echo Running drug distribution project using MPI...
echo.

REM ✅ Check if process_count.txt exists
IF NOT EXIST process_count.txt (
    echo The file process_count.txt does not exist. Please run the program first.
    pause
    exit /b
)

REM ✅ Read number of total ranks
set /p totalRanks=<process_count.txt

echo %totalRanks% processes will be run.

REM ✅ Set path to the executable (relative to Runner folder)
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
