$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'Entities.Authoring.Generator.csproj') --configuration Release --nologo -p:CopyGeneratorToPackage=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
