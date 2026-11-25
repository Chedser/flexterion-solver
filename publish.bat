@echo off
chcp 65001 > nul
if exist ".\publish" rmdir /s /q ".\publish"
dotnet publish MechCore.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
pause
