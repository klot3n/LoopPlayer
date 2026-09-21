# Сборка самостоятельного LoopPlayer.exe (win-x64, single-file, self-contained).
# Требуется .NET SDK 10: winget install Microsoft.DotNet.SDK.10
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test tests/LoopPlayer.Tests -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Тесты не прошли" }

dotnet publish src/LoopPlayer/LoopPlayer.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o dist --nologo
if ($LASTEXITCODE -ne 0) { throw "Публикация не удалась" }

Write-Host "`nГотово: $PSScriptRoot\dist\LoopPlayer.exe"
