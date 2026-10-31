param(
    [string]$PackagePath,
    [string]$SnapshotPath,
    [string]$ProjectPath,
    [string]$LogDirectory,
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$PackagePath = [System.IO.Path]::GetFullPath($PackagePath)
$SnapshotPath = [System.IO.Path]::GetFullPath($SnapshotPath)
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$LogDirectory = [System.IO.Path]::GetFullPath($LogDirectory)
if (Test-Path -LiteralPath $SnapshotPath) { throw 'Use a fresh, isolated Git fixture directory.' }
$packageSourceRoot = [System.IO.Path]::GetFullPath($PackagePath)
$snapshotPackage = Join-Path $SnapshotPath 'Packages/com.seikasan.entities.authoring'
New-Item -ItemType Directory -Path $snapshotPackage -Force | Out-Null
Get-ChildItem -LiteralPath $packageSourceRoot -Recurse -File -Force | ForEach-Object {
    $relative = $_.FullName.Substring($packageSourceRoot.Length + 1).Replace('\', '/')
    if ($relative -notmatch '(^|/)(bin|obj)/') {
        $destination = Join-Path $snapshotPackage $relative
        New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
}
git init --initial-branch=codex/validation $SnapshotPath
if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize isolated Git fixture.' }
git -C $SnapshotPath add .
git -C $SnapshotPath -c user.name='Entities Authoring Validation' -c user.email=validation@localhost commit -m 'Isolated package installation fixture'
if ($LASTEXITCODE -ne 0) { throw 'Cannot create isolated validation snapshot.' }
$snapshotRevision = git -C $SnapshotPath rev-parse HEAD
$snapshotUri = ([System.Uri]::new([System.IO.Path]::GetFullPath($SnapshotPath))).AbsoluteUri
$gitDependency = "git+$snapshotUri`?path=/Packages/com.seikasan.entities.authoring#$snapshotRevision"
$manifestFile = Join-Path $ProjectPath 'Packages/manifest.json'
$lockFile = Join-Path $ProjectPath 'Packages/packages-lock.json'
$originalManifestText = [System.IO.File]::ReadAllText($manifestFile)
$originalLockText = [System.IO.File]::ReadAllText($lockFile)
try {
    $gitManifest = $originalManifestText | ConvertFrom-Json
    $gitManifest.dependencies.'com.seikasan.entities.authoring' = $gitDependency
    $testables = @($gitManifest.testables)
    if ($testables -notcontains 'com.seikasan.entities.authoring') {
        $gitManifest | Add-Member -MemberType NoteProperty -Name testables -Value @($testables + 'com.seikasan.entities.authoring') -Force
    }

    [System.IO.File]::WriteAllText($manifestFile, ($gitManifest | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
    $testResultsFile = Join-Path $LogDirectory 'EntitiesAuthoring-Git-Tests.xml'
    $gitValidation = Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-nographics','-projectPath',$ProjectPath,'-runTests','-testPlatform','EditMode','-testFilter','Entities.Authoring.Tests.Editor','-testResults',$testResultsFile,'-logFile',(Join-Path $LogDirectory 'EntitiesAuthoring-Git-Install.log')) -WindowStyle Hidden -PassThru
    $gitValidation.WaitForExit()
    if ($gitValidation.ExitCode -ne 0) { throw "Git-package Unity validation failed ($($gitValidation.ExitCode))." }

    if (-not (Test-Path -LiteralPath $testResultsFile)) { throw 'Unity did not produce a test result XML file.' }
    [xml]$testResults = [System.IO.File]::ReadAllText($testResultsFile)
    $testRun = $testResults.SelectSingleNode('/test-run')
    if ($null -eq $testRun) { throw 'Unity test result XML does not contain a test-run summary.' }
    $passed = [int]$testRun.passed
    $failed = [int]$testRun.failed
    $total = [int]$testRun.total
    if ($testRun.result -ne 'Passed' -or $failed -ne 0 -or $passed -eq 0 -or $total -ne $passed + [int]$testRun.skipped + [int]$testRun.inconclusive) {
        throw "Git-package Unity tests did not all pass (passed=$passed, failed=$failed, total=$total, result=$($testRun.result))."
    }

    $resolvedLock = Get-Content -LiteralPath (Join-Path $ProjectPath 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
    if ($resolvedLock.dependencies.'com.seikasan.entities.authoring'.source -ne 'git') { throw 'UPM did not resolve the dependency as Git.' }
    Write-Output "AEA_GIT_INSTALL_PASSED revision=$snapshotRevision tests=$passed/$total url=$gitDependency"
}
finally {
    [System.IO.File]::WriteAllText($manifestFile, $originalManifestText, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($lockFile, $originalLockText, [System.Text.UTF8Encoding]::new($false))
}
