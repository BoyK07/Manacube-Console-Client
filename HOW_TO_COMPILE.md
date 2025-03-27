- Dotnet 7.0

# Restore all NuGet packages
dotnet restore MinecraftClient/MinecraftClient.csproj

# Build the project in Release configuration
dotnet build MinecraftClient/MinecraftClient.csproj -c Release

# Publish a self-contained executable for Windows (x64)
dotnet publish MinecraftClient/MinecraftClient.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# Publish a self-contained executable for Linux (x64)
dotnet publish MinecraftClient/MinecraftClient.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true