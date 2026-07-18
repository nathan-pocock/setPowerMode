@echo off
setlocal

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo Could not find csc.exe. Is the .NET Framework 4.x installed?
    exit /b 1
)

"%CSC%" /nologo /target:winexe /out:PowerModeTray.exe ^
    /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll ^
    PowerModeTray.cs

endlocal
