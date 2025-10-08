@(set '(=)||' <# indeed a lean and mean cmd / powershell hybrid #> @'

::# Based on Skip TPM Check on Dynamic Update V13 by AveYo
::(https://github.com/AveYo/MediaCreationTool.bat/blob/main/bypass11/Skip_TPM_Check_on_Dynamic_Update.cmd)

@echo off & title Checking patch status...
if /i "%~f0" neq "%SystemDrive%\Scripts\get11.cmd" goto setup
powershell -win 1 -nop -c ";"
set CLI=%*& set SOURCES=%SystemDrive%\$WINDOWS.~BT\Sources& set MEDIA=.& set MOD=CLI& set PRE=WUA& set /a VER=11
if not defined CLI (exit /b) else if not exist %SOURCES%\SetupHost.exe (exit /b)
if not exist %SOURCES%\WindowsUpdateBox.exe mklink /h %SOURCES%\WindowsUpdateBox.exe %SOURCES%\SetupHost.exe
reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /f /v DisableWUfBSafeguards /d 1 /t reg_dword
reg add HKLM\SYSTEM\Setup\MoSetup /f /v AllowUpgradesWithUnsupportedTPMorCPU /d 1 /t reg_dword
set OPT=/Compat IgnoreWarning /MigrateDrivers All /Telemetry Disable
set /a restart_application=0x800705BB & (call set CLI=%%CLI:%1 =%%)
set /a incorrect_parameter=0x80070057 & (set SRV=%CLI:/Product Client =%)
set /a launch_option_error=0xc190010a & (set SRV=%SRV:/Product Server =%)
for %%W in (%CLI%) do if /i %%W == /PreDownload (set MOD=SRV)
for %%W in (%CLI%) do if /i %%W == /InstallFile (set PRE=ISO& set "MEDIA=") else if not defined MEDIA set "MEDIA=%%~dpW"
if %VER% == 11 for %%W in ("%MEDIA%appraiserres.dll") do if exist %%W if %%~zW == 0 set AlreadyPatched=1 & set /a VER=10
if %VER% == 11 findstr /r "P.r.o.d.u.c.t.V.e.r.s.i.o.n...1.0.\..0.\..2.[2-9]" %SOURCES%\SetupHost.exe >nul 2>nul || set /a VER=10
if %VER% == 11 if not exist "%MEDIA%EI.cfg" (echo;[Channel]>%SOURCES%\EI.cfg & echo;_Default>>%SOURCES%\EI.cfg)
if %VER%_%PRE% == 11_ISO (%SOURCES%\WindowsUpdateBox.exe /Product Server /PreDownload /Quiet %OPT%)
if %VER%_%PRE% == 11_ISO (del /f /q %SOURCES%\appraiserres.dll 2>nul & cd.>%SOURCES%\appraiserres.dll & call :canary)
if %VER%_%MOD% == 11_SRV (set ARG=%OPT% %SRV% /Product Server)
if %VER%_%MOD% == 11_CLI (set ARG=%OPT% %CLI%)
%SOURCES%\WindowsUpdateBox.exe %ARG%
if %errorlevel% == %restart_application% (call :canary & %SOURCES%\WindowsUpdateBox.exe %ARG%)
exit /b

:canary iso skip 2nd tpm check by AveYo  
set C=  $X='%SOURCES%\hwreqchk.dll'; $Y='SQ_TpmVersion GTE 1'; $Z='SQ_TpmVersion GTE 0'; if (test-path $X) { 
set C=%C%  try { takeown.exe /f $X /a; icacls.exe $X /grant *S-1-5-32-544:f; attrib -R -S $X; [io.file]::OpenWrite($X).close() }
set C=%C%  catch { return }; $R=[Text.Encoding]::UTF8.GetBytes($Z); $l=$R.Length; $i=2; $w=!1;
set C=%C%  $B=[io.file]::ReadAllBytes($X); $H=[BitConverter]::ToString($B) -replace '-';
set C=%C%  $S=[BitConverter]::ToString([Text.Encoding]::UTF8.GetBytes($Y)) -replace '-';
set C=%C%  do { $i=$H.IndexOf($S, $i + 2); if ($i -gt 0) { $w=!0; for ($k=0; $k -lt $l; $k++) { $B[$k + $i / 2]=$R[$k] } } }
set C=%C%  until ($i -lt 1); if ($w) { [io.file]::WriteAllBytes($X, $B); [GC]::Collect() } }
if %VER%_%PRE% == 11_ISO powershell -nop -c iex($env:C) >nul 2>nul
exit /b

:setup
::Install or remove if "patch" argument is present, else relay install status
set CLI=%*& (set IFEO=HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options)
wmic /namespace:"\\root\subscription" path __EventFilter where Name="Skip TPM Check on Dynamic Update" delete >nul 2>nul & rem v1
reg delete "%IFEO%\vdsldr.exe" /f 2>nul & rem v2 - v5
if /i "%CLI%"=="" reg query "%IFEO%\SetupHost.exe\0" /v Debugger >nul 2>nul && goto checkistrue || goto checkisfalse
if /i "%~1"=="patch" reg query "%IFEO%\SetupHost.exe\0" /v Debugger >nul 2>nul && goto remove || goto install

:checkisfalse
::Patch has not been detected
powershell -Command "$p=new-object System.IO.Pipes.NamedPipeClientStream('.', 'UnGate11Pipe', [IO.Pipes.PipeDirection]::Out); $p.Connect(); $sw=new-object System.IO.StreamWriter($p); $sw.AutoFlush=$true; $sw.WriteLine('C1'); $sw.Close(); $p.Close()"
exit /b

:checkistrue
::Patch has been detected
powershell -Command "$p=new-object System.IO.Pipes.NamedPipeClientStream('.', 'UnGate11Pipe', [IO.Pipes.PipeDirection]::Out); $p.Connect(); $sw=new-object System.IO.StreamWriter($p); $sw.AutoFlush=$true; $sw.WriteLine('C0'); $sw.Close(); $p.Close()"
exit /b

:install
title Patching
mkdir %SystemDrive%\Scripts >nul 2>nul & copy /y "%~f0" "%SystemDrive%\Scripts\get11.cmd" >nul 2>nul
reg add "%IFEO%\SetupHost.exe" /f /v UseFilter /d 1 /t reg_dword >nul
reg add "%IFEO%\SetupHost.exe\0" /f /v FilterFullPath /d "%SystemDrive%\$WINDOWS.~BT\Sources\SetupHost.exe" >nul
reg add "%IFEO%\SetupHost.exe\0" /f /v Debugger /d "%SystemDrive%\Scripts\get11.cmd" >nul
::Patch was installed
powershell -Command "$p=new-object System.IO.Pipes.NamedPipeClientStream('.', 'UnGate11Pipe', [IO.Pipes.PipeDirection]::Out); $p.Connect(); $sw=new-object System.IO.StreamWriter($p); $sw.AutoFlush=$true; $sw.WriteLine('P0'); $sw.Close(); $p.Close()"

exit /b

:remove
title Unpatching
del /f /q "%SystemDrive%\Scripts\get11.cmd" "%Public%\get11.cmd" "%ProgramData%\get11.cmd" >nul 2>nul
reg delete "%IFEO%\SetupHost.exe" /f >nul 2>nul
::Patch was removed
powershell -Command "$p=new-object System.IO.Pipes.NamedPipeClientStream('.', 'UnGate11Pipe', [IO.Pipes.PipeDirection]::Out); $p.Connect(); $sw=new-object System.IO.StreamWriter($p); $sw.AutoFlush=$true; $sw.WriteLine('P1'); $sw.Close(); $p.Close()"

exit /b
