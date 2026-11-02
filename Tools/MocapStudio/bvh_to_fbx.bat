@echo off
rem Keo tha 1 hoac nhieu file .bvh vao file nay de doi sang .fbx (can Blender).
rem Neu Blender cai o cho khac: dat bien moi truong BLENDER_EXE = duong dan blender.exe
setlocal
set "BLENDER=%BLENDER_EXE%"
if not defined BLENDER for /d %%D in ("%ProgramFiles%\Blender Foundation\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER for /d %%D in ("%ProgramFiles(x86)%\Steam\steamapps\common\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER (
  echo Khong tim thay Blender. Cai Blender hoac dat BLENDER_EXE.
  pause & exit /b 1
)
if "%~1"=="" (
  echo Keo tha file .bvh vao file nay.
  pause & exit /b 1
)
echo Dung Blender: %BLENDER%
"%BLENDER%" -b --factory-startup -P "%~dp0bvh_to_fbx.py" -- %*
pause
