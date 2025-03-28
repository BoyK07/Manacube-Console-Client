@echo on

REM Restore the project
dotnet restore MinecraftClient/MinecraftClient.csproj || exit /b

REM Build the project in Release configuration
dotnet build MinecraftClient/MinecraftClient.csproj -c Release || exit /b

REM Publish a self-contained single-file executable for Windows (x64)
dotnet publish MinecraftClient/MinecraftClient.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true || exit /b

echo Build complete!
pause
