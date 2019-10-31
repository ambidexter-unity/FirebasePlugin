@echo off
idea64.exe >nul 2>&1 && (
    echo Opening .\Assets\Server~
    idea64.exe .\Assets\Server~
) || (
    echo Add the path to the IntelliJ IDEA bin folder to the Path environment variable (for example, C:\Program Files\JetBrains\IntelliJ IDEA\bin)
)