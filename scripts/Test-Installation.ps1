[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('bom-install-test-' + [Guid]::NewGuid().ToString('N'))
$appData = Join-Path $testRoot 'Roaming'
$commonPlugins = Join-Path $testRoot 'CommonPlugins'
$installer = Join-Path $PSScriptRoot 'Install-Local.ps1'
$rulesSource = Join-Path $repositoryRoot 'userdata\BomCadPlugin\component-rules.json'
$sourceHash = (Get-FileHash -LiteralPath $rulesSource).Hash
$rulesTarget = Join-Path $appData 'BomCadPlugin\component-rules.json'
$pluginsPath = Join-Path $appData 'Autodesk\ApplicationPlugins'

function Assert-Test([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

foreach ($version in @('2018', '2019', '2020', '2021', '2022', '2023', '2024', '2025')) {
    & $installer -AutoCADVersion $version -AppDataPath $appData -CommonPluginsPath $commonPlugins -InformationAction SilentlyContinue | Out-Null
    $expectedBundle = if ($version -eq '2025') { 'BomCadPlugin-2025.bundle' } else { 'BomCadPlugin-2018-2024.bundle' }
    $bundles = @(Get-ChildItem -LiteralPath $pluginsPath -Directory)
    Assert-Test ($bundles.Count -eq 1 -and $bundles[0].Name -eq $expectedBundle) "Wrong installed bundle for $version."
    Assert-Test ((Get-FileHash -LiteralPath $rulesTarget).Hash -eq $sourceHash) "Rules changed during $version installation."
}

# Reinstallation preserves a genuinely different existing rule library before replacing it.
[IO.File]::WriteAllText($rulesTarget, '{"version":"old-local-data","rules":[]}')
$oldRulesHash = (Get-FileHash -LiteralPath $rulesTarget).Hash
$oldBundlePath = Join-Path $pluginsPath 'BomCadPlugin.bundle'
New-Item -ItemType Directory -Path $oldBundlePath -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $oldBundlePath 'old-plugin.txt'), 'previous plugin')
& $installer -AutoCADVersion 2025 -AppDataPath $appData -CommonPluginsPath $commonPlugins -InformationAction SilentlyContinue | Out-Null
$backupsPath = Join-Path $appData 'BomCadPlugin\backups'
$savedRules = @(Get-ChildItem -LiteralPath $backupsPath -Recurse -Filter component-rules.json |
    Where-Object { (Get-FileHash -LiteralPath $_.FullName).Hash -eq $oldRulesHash })
Assert-Test ($savedRules.Count -eq 1) 'Previous rules were not preserved.'
Assert-Test (@(Get-ChildItem -LiteralPath $backupsPath -Recurse -Filter old-plugin.txt).Count -eq 1) 'Previous bundle was not preserved.'
Assert-Test (-not (Test-Path -LiteralPath $oldBundlePath)) 'Old bundle remains in the AutoCAD loader directory.'
Assert-Test ((Get-FileHash -LiteralPath $rulesTarget).Hash -eq $sourceHash) 'Reinstallation did not restore the snapshot.'

# Conflicting machine-wide installation and invalid CAD version must fail before changes.
New-Item -ItemType Directory -Path (Join-Path $commonPlugins 'BomCadPlugin.bundle') -Force | Out-Null
$backupsBefore = @(Get-ChildItem -LiteralPath $backupsPath -Directory).Count
foreach ($version in @('2025', '2026')) {
    $failed = $false
    try {
        & $installer -AutoCADVersion $version -AppDataPath $appData -CommonPluginsPath $commonPlugins | Out-Null
    }
    catch {
        $failed = $true
        $expected = if ($version -eq '2025') { '*machine-wide plugin*' } else { '*Unsupported AutoCAD version*' }
        Assert-Test ($_.Exception.Message -like $expected) "Unexpected failure: $($_.Exception.Message)"
    }
    Assert-Test $failed "Expected installation rejection for $version."
}
Assert-Test (@(Get-ChildItem -LiteralPath $backupsPath -Directory).Count -eq $backupsBefore) 'Rejected installation changed backups.'
Assert-Test ((Get-FileHash -LiteralPath $rulesTarget).Hash -eq $sourceHash) 'Rejected installation changed rules.'
Assert-Test ((Get-FileHash -LiteralPath $rulesSource).Hash -eq $sourceHash) 'Source snapshot was modified.'
Write-Output 'Installation validation passed: 8 CAD versions, exact rule restore, backups, conflict rejection, invalid version rejection.'
Write-Output "Isolated test files: $testRoot"
