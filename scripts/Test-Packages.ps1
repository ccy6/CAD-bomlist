[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $DistPath = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RuntimeRange {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement] $ComponentEntry
    )

    $requirements = $ComponentEntry.RuntimeRequirements
    Assert-Condition ($null -ne $requirements) "Component '$($ComponentEntry.AppName)' has no RuntimeRequirements."
    return "$($requirements.SeriesMin)-$($requirements.SeriesMax)"
}

$resolvedDistPath = [System.IO.Path]::GetFullPath($DistPath)
Assert-Condition (Test-Path -LiteralPath $resolvedDistPath -PathType Container) "Distribution directory does not exist: $resolvedDistPath"

$topLevelEntries = @(Get-ChildItem -LiteralPath $resolvedDistPath)
$bundles = @($topLevelEntries | Where-Object { $_.PSIsContainer -and $_.Name -like '*.bundle' })
Assert-Condition ($topLevelEntries.Count -eq 2) "Distribution directory must contain only the two release bundles; found $($topLevelEntries.Count) top-level entries."
Assert-Condition ($bundles.Count -eq 2) "Expected exactly two .bundle directories in '$resolvedDistPath'; found $($bundles.Count)."

$expectedBundles = @{
    'BomCadPlugin-2018-2024.bundle' = @('R22.0-R22.0', 'R23.0-R23.1', 'R24.0-R24.3')
    'BomCadPlugin-2025.bundle' = @('R25.0-R25.0')
}

$expectedCommands = @(
    'BOM_PARAMS',
    'BOM_ADD_RULE',
    'BOM_RULES',
    'BOM_STAT',
    'BOM_EXPORT',
    'BOM_CLEAR',
    'BOM_ABOUT'
)

$legacyRuntimeDependencies = @(
    'BomCadPlugin.Core.dll',
    'Microsoft.Bcl.AsyncInterfaces.dll',
    'System.Buffers.dll',
    'System.Memory.dll',
    'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll',
    'System.Text.Encodings.Web.dll',
    'System.Text.Json.dll',
    'System.Threading.Tasks.Extensions.dll',
    'System.ValueTuple.dll'
)

foreach ($bundleName in $expectedBundles.Keys) {
    $bundlePath = Join-Path $resolvedDistPath $bundleName
    Assert-Condition (Test-Path -LiteralPath $bundlePath -PathType Container) "Missing bundle: $bundleName"

    $manifestPath = Join-Path $bundlePath 'PackageContents.xml'
    Assert-Condition (Test-Path -LiteralPath $manifestPath -PathType Leaf) "Missing manifest: $manifestPath"

    [xml] $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8
    $entries = @($manifest.ApplicationPackage.Components.ComponentEntry)
    $actualRanges = @($entries | ForEach-Object { Get-RuntimeRange $_ })
    $expectedRanges = $expectedBundles[$bundleName]
    Assert-Condition ($actualRanges.Count -eq $expectedRanges.Count) "Bundle '$bundleName' has $($actualRanges.Count) runtime rows; expected $($expectedRanges.Count)."

    for ($index = 0; $index -lt $expectedRanges.Count; $index++) {
        Assert-Condition ($actualRanges[$index] -eq $expectedRanges[$index]) "Bundle '$bundleName' runtime row $index is '$($actualRanges[$index])'; expected '$($expectedRanges[$index])'."
    }

    foreach ($entry in $entries) {
        $moduleName = [string] $entry.ModuleName
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($moduleName)) "Component '$($entry.AppName)' has no ModuleName."

        $relativeModuleName = $moduleName.Replace('/', [System.IO.Path]::DirectorySeparatorChar).TrimStart('.', [System.IO.Path]::DirectorySeparatorChar)
        $modulePath = Join-Path $bundlePath $relativeModuleName
        Assert-Condition (Test-Path -LiteralPath $modulePath -PathType Leaf) "Manifest module does not exist: $modulePath"

        $moduleDirectory = Split-Path -Parent $modulePath
        $requiredRuntimeDependencies = @('BomCadPlugin.Core.dll')
        if ($bundleName -eq 'BomCadPlugin-2018-2024.bundle') {
            $requiredRuntimeDependencies = $legacyRuntimeDependencies
        }

        foreach ($dependencyName in $requiredRuntimeDependencies) {
            $dependencyPath = Join-Path $moduleDirectory $dependencyName
            Assert-Condition (Test-Path -LiteralPath $dependencyPath -PathType Leaf) "Component '$($entry.AppName)' is missing runtime dependency '$dependencyName'."
        }

        $commands = @($entry.Commands.Command | ForEach-Object { [string] $_.Global })
        Assert-Condition ($commands.Count -eq $expectedCommands.Count) "Component '$($entry.AppName)' must declare exactly $($expectedCommands.Count) commands; found $($commands.Count)."
        foreach ($command in $expectedCommands) {
            Assert-Condition ($commands -contains $command) "Component '$($entry.AppName)' is missing command '$command'."
        }
    }
}

$autodeskHostDllNames = @('AcMgd.dll', 'AcDbMgd.dll', 'AcCoreMgd.dll', 'AdWindows.dll')
$packagedHostDlls = @(Get-ChildItem -LiteralPath $resolvedDistPath -Recurse -File | Where-Object { $autodeskHostDllNames -contains $_.Name })
Assert-Condition ($packagedHostDlls.Count -eq 0) "Autodesk host DLLs must not be packaged: $($packagedHostDlls.FullName -join ', ')"

Write-Output "Package validation passed: $resolvedDistPath"
