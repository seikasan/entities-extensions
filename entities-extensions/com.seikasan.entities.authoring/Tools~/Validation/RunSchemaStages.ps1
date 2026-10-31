param(
    [string]$ProjectPath,
    [string]$LogDirectory,
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
function Invoke-ValidationUnity([string]$Method, [string]$LogName) {
    $validationChild = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-nographics','-projectPath',$ProjectPath,'-executeMethod',$Method,'-logFile',(Join-Path $LogDirectory $LogName)) -WindowStyle Hidden -PassThru
    $validationChild.WaitForExit()
    if ($validationChild.ExitCode -ne 0) { throw "Validation failed: $LogName ($($validationChild.ExitCode))" }
    Write-Output "PASS $LogName"
}
$schemaSource = Join-Path $ProjectPath 'Assets/EntitiesAuthoringSchema/SchemaProbe.cs'
if (!(Test-Path -LiteralPath $schemaSource)) {
    $fixtureRoot = [System.IO.Path]::GetDirectoryName($schemaSource)
    $fixtureEditor = Join-Path $fixtureRoot 'Editor'
    New-Item -ItemType Directory -Path $fixtureEditor -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SchemaFixture/SchemaProbe.cs') -Destination $schemaSource
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SchemaFixture/Entities.Authoring.Tests.Schema.asmdef') -Destination $fixtureRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SchemaFixture/Entities.Authoring.SchemaValidation.asmdef') -Destination $fixtureEditor
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SchemaValidation.cs') -Destination $fixtureEditor
}
Invoke-ValidationUnity 'Entities.Authoring.Tests.Editor.ValidationBootstrap.Warmup' 'EntitiesAuthoring-Schema-Initial-Warmup.log'
Invoke-ValidationUnity 'Entities.Authoring.Validation.SchemaValidation.CreateBaseline' 'EntitiesAuthoring-Schema-Baseline.log'
$updatedSchema = @'
using Entities.Authoring;
using Unity.Entities;
namespace Entities.Authoring.Tests.Schema.First
{
    [AutoAuthoring(TransformUsageFlags.Dynamic)]
    public struct SchemaProbe : IComponentData
    {
        public float Value;
        public int Added;
    }
}
'@
[System.IO.File]::WriteAllText($schemaSource, $updatedSchema, [System.Text.UTF8Encoding]::new($false))
Invoke-ValidationUnity 'Entities.Authoring.Tests.Editor.ValidationBootstrap.Warmup' 'EntitiesAuthoring-Schema-Fields-Warmup.log'
Invoke-ValidationUnity 'Entities.Authoring.Validation.SchemaValidation.VerifyChangedSchema' 'EntitiesAuthoring-Schema-Fields.log'
[System.IO.File]::WriteAllText($schemaSource, $updatedSchema.Replace('Schema.First', 'Schema.Second'), [System.Text.UTF8Encoding]::new($false))
Invoke-ValidationUnity 'Entities.Authoring.Tests.Editor.ValidationBootstrap.Warmup' 'EntitiesAuthoring-Schema-Namespace-Warmup.log'
Invoke-ValidationUnity 'Entities.Authoring.Validation.SchemaValidation.VerifyChangedSchema' 'EntitiesAuthoring-Schema-Namespace.log'
[System.IO.File]::WriteAllText($schemaSource, '// Component deliberately removed by the isolated regression fixture.', [System.Text.UTF8Encoding]::new($false))
Invoke-ValidationUnity 'Entities.Authoring.Tests.Editor.ValidationBootstrap.Warmup' 'EntitiesAuthoring-Schema-Orphan-Warmup.log'
Invoke-ValidationUnity 'Entities.Authoring.Validation.SchemaValidation.VerifyOrphan' 'EntitiesAuthoring-Schema-Orphan.log'
Write-Output 'AEA_SCHEMA_STAGES_PASSED'
