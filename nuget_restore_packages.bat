@echo off
set path=%path%;%cd%\.nuget
for %%f in (*.sln) do nuget restore %%f
pause