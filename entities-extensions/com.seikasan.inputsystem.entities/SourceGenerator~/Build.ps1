$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'InputSystem.Entities.SourceGenerator.csproj') --configuration Release --nologo -p:CopyGeneratorToPackage=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
