[CmdletBinding()]
param(
    [string] $AutoCADVersion,
    [string] $AppDataPath = [Environment]::GetFolderPath('ApplicationData'),
    [string] $CommonPluginsPath = (Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$supportedVersions = @('2018', '2019', '2020', '2021', '2022', '2023', '2024', '2025')
if ([string]::IsNullOrWhiteSpace($AutoCADVersion)) {
    $AutoCADVersion = Read-Host 'AutoCAD version (2018-2025)'
}
if ($supportedVersions -notcontains $AutoCADVersion) {
    throw "Unsupported AutoCAD version: $AutoCADVersion. Use 2018 through 2025 (not AutoCAD LT)."
}
if ([string]::IsNullOrWhiteSpace($AppDataPath) -or -not [IO.Path]::IsPathRooted($AppDataPath)) {
    throw 'AppDataPath must be an absolute directory path.'
}
$AppDataPath = [IO.Path]::GetFullPath($AppDataPath)
if ($AppDataPath -eq [Environment]::GetFolderPath('ApplicationData') -and (Get-Process acad -ErrorAction SilentlyContinue)) {
    throw 'Close AutoCAD before installing, then run this installer again.'
}

$bundleNames = @('BomCadPlugin.bundle', 'BomCadPlugin-2018-2024.bundle', 'BomCadPlugin-2025.bundle')
foreach ($name in $bundleNames) {
    if (Test-Path -LiteralPath (Join-Path $CommonPluginsPath $name)) {
        throw "An older machine-wide plugin exists at '$CommonPluginsPath\$name'. Back it up and move it outside ApplicationPlugins first (administrator access may be required), then rerun this installer."
    }
}

& (Join-Path $PSScriptRoot 'Test-Packages.ps1') -DistPath (Join-Path $repositoryRoot 'dist')
$rulesSource = Join-Path $repositoryRoot 'userdata\BomCadPlugin\component-rules.json'
$library = Get-Content -LiteralPath $rulesSource -Raw -Encoding UTF8 | ConvertFrom-Json
if (@($library.productSystems).Count -eq 0 -or @($library.rules).Count -eq 0) {
    throw 'The bundled rule library has no systems or rules.'
}
$bundleName = if ($AutoCADVersion -eq '2025') { 'BomCadPlugin-2025.bundle' } else { 'BomCadPlugin-2018-2024.bundle' }
$sourceBundle = Join-Path $repositoryRoot "dist\$bundleName"
$pluginsPath = Join-Path $AppDataPath 'Autodesk\ApplicationPlugins'
$dataPath = Join-Path $AppDataPath 'BomCadPlugin'
$backupPath = Join-Path $dataPath ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$rulesTarget = Join-Path $dataPath 'component-rules.json'
New-Item -ItemType Directory -Path $pluginsPath, $dataPath, $backupPath -Force | Out-Null

# Preserve existing user data and move only the known BOM bundles out of the loader directory.
if (Test-Path -LiteralPath $rulesTarget) {
    Copy-Item -LiteralPath $rulesTarget -Destination (Join-Path $backupPath 'component-rules.json')
}
$pluginsPrefix = [IO.Path]::GetFullPath($pluginsPath).TrimEnd('\') + '\'
foreach ($name in $bundleNames) {
    $existingBundle = [IO.Path]::GetFullPath((Join-Path $pluginsPath $name))
    $savedBundle = [IO.Path]::GetFullPath((Join-Path $backupPath $name))
    if (-not $existingBundle.StartsWith($pluginsPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not $savedBundle.StartsWith($backupPath.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to move a bundle outside the installation or backup directory.'
    }
    if (Test-Path -LiteralPath $existingBundle) {
        Move-Item -LiteralPath $existingBundle -Destination $savedBundle
    }
}
Copy-Item -LiteralPath $sourceBundle -Destination $pluginsPath -Recurse
Copy-Item -LiteralPath $rulesSource -Destination $rulesTarget -Force

$targetBundle = Join-Path $pluginsPath $bundleName
foreach ($file in Get-ChildItem -LiteralPath $sourceBundle -Recurse -File) {
    $relativePath = $file.FullName.Substring($sourceBundle.Length + 1)
    $targetFile = Join-Path $targetBundle $relativePath
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $targetFile).Hash) {
        throw "Installed file verification failed: $targetFile. Backups: $backupPath"
    }
}
if ((Get-FileHash -LiteralPath $rulesSource).Hash -ne (Get-FileHash -LiteralPath $rulesTarget).Hash) {
    throw "Rule library verification failed. Backups: $backupPath"
}
Write-Host "Installed: $targetBundle"
Write-Host "Restored $(@($library.productSystems).Count) systems and $(@($library.rules).Count) rules: $rulesTarget"
Write-Host "Previous files (if any) are preserved in: $backupPath"
Write-Host 'Start AutoCAD and run BOM_RULES to inspect the restored library.'
