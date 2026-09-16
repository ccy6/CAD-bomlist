[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $AutoCAD2018ReferencePath,

    [Parameter(Mandatory = $false)]
    [string] $AutoCAD2019ReferencePath,

    [Parameter(Mandatory = $false)]
    [string] $AutoCAD2021ReferencePath,

    [Parameter(Mandatory = $false)]
    [string] $AutoCAD2025ReferencePath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsPath = Join-Path $repositoryRoot 'artifacts'
$distPath = Join-Path $repositoryRoot 'dist'
$projectPath = Join-Path $repositoryRoot 'src\BomCadPlugin\BomCadPlugin.csproj'
$packagingPath = Join-Path $repositoryRoot 'packaging'
$autodeskHostDllNames = @('AcMgd.dll', 'AcDbMgd.dll', 'AcCoreMgd.dll', 'AdWindows.dll')
$expectedManagedApiVersions = @{
    '2018' = '22.0'
    '2019' = '23.0'
    '2021' = '24.0'
    '2025' = '25.0'
}

function Resolve-AutoCADReferencePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $false)]
        [string] $SuppliedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($SuppliedPath)) {
        if (-not (Test-Path -LiteralPath $SuppliedPath -PathType Container)) {
            throw "The supplied AutoCAD $Version reference directory does not exist: $SuppliedPath"
        }

        return [System.IO.Path]::GetFullPath($SuppliedPath)
    }

    $candidates = @("C:\Program Files\Autodesk\AutoCAD $Version")
    if ($Version -eq '2025') {
        $candidates += 'C:\1 Software\cad2025\AutoCAD 2025'
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "AutoCAD $Version reference directory was not found. Pass -AutoCAD${Version}ReferencePath with the matching AutoCAD installation directory. Checked: $($candidates -join ', ')"
}

function Assert-AutoCADReferences {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $true)]
        [string] $ReferencePath
    )

    $missingDlls = @($autodeskHostDllNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $ReferencePath $_) -PathType Leaf) })
    if ($missingDlls.Count -gt 0) {
        throw "AutoCAD $Version managed references are incomplete in '$ReferencePath'. Missing: $($missingDlls -join ', ')"
    }

    $expectedManagedApiVersion = $expectedManagedApiVersions[$Version]
    foreach ($managedApiDllName in @('AcMgd.dll', 'AcDbMgd.dll', 'AcCoreMgd.dll')) {
        $managedApiDllPath = Join-Path $ReferencePath $managedApiDllName
        $actualFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($managedApiDllPath).FileVersion
        $versionParts = @($actualFileVersion -split '\.')
        $actualManagedApiVersion = "$($versionParts[0]).$($versionParts[1])"
        if ($actualManagedApiVersion -ne $expectedManagedApiVersion) {
            throw "AutoCAD $Version requires $managedApiDllName version $expectedManagedApiVersion.x, but '$managedApiDllPath' is version $actualFileVersion. Use the matching AutoCAD SDK directory."
        }
    }
}

function Reset-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a directory outside the repository: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $resolvedPath | Out-Null
}

function Invoke-PluginBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version,

        [Parameter(Mandatory = $true)]
        [string] $ReferencePath
    )

    $outputPath = Join-Path $artifactsPath "$Version\output"
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

    $arguments = @(
        'build',
        $projectPath,
        '--configuration',
        'Release',
        '--no-incremental',
        "-p:AutoCADVersion=$Version",
        "-p:AutoCADReferencePath=$ReferencePath",
        "-p:OutputPath=$outputPath"
    )

    Write-Output "Building AutoCAD $Version with references from '$ReferencePath'..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "AutoCAD $Version build failed with exit code $LASTEXITCODE."
    }

    foreach ($requiredOutput in @('BomCadPlugin.dll', 'BomCadPlugin.Core.dll')) {
        $requiredOutputPath = Join-Path $outputPath $requiredOutput
        if (-not (Test-Path -LiteralPath $requiredOutputPath -PathType Leaf)) {
            throw "AutoCAD $Version build did not produce '$requiredOutputPath'."
        }
    }

    return $outputPath
}

function Copy-BuildOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourcePath,

        [Parameter(Mandatory = $true)]
        [string] $DestinationPath
    )

    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
    Get-ChildItem -LiteralPath $SourcePath -File | Where-Object { $autodeskHostDllNames -notcontains $_.Name } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DestinationPath -Force
    }
}

$referencePaths = @{
    '2018' = Resolve-AutoCADReferencePath '2018' $AutoCAD2018ReferencePath
    '2019' = Resolve-AutoCADReferencePath '2019' $AutoCAD2019ReferencePath
    '2021' = Resolve-AutoCADReferencePath '2021' $AutoCAD2021ReferencePath
    '2025' = Resolve-AutoCADReferencePath '2025' $AutoCAD2025ReferencePath
}

foreach ($version in @('2018', '2019', '2021', '2025')) {
    Assert-AutoCADReferences $version $referencePaths[$version]
}

Reset-GeneratedDirectory $artifactsPath
Reset-GeneratedDirectory $distPath

$buildOutputs = @{}
foreach ($version in @('2018', '2019', '2021', '2025')) {
    $buildOutputs[$version] = Invoke-PluginBuild $version $referencePaths[$version]
}

$legacyBundlePath = Join-Path $distPath 'BomCadPlugin-2018-2024.bundle'
$modernBundlePath = Join-Path $distPath 'BomCadPlugin-2025.bundle'
Copy-Item -LiteralPath (Join-Path $packagingPath 'BomCadPlugin-2018-2024.bundle') -Destination $distPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $packagingPath 'BomCadPlugin-2025.bundle') -Destination $distPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $packagingPath 'README.txt') -Destination $legacyBundlePath -Force
Copy-Item -LiteralPath (Join-Path $packagingPath 'README.txt') -Destination $modernBundlePath -Force

Copy-BuildOutput $buildOutputs['2018'] (Join-Path $legacyBundlePath 'Contents\Windows\2018')
Copy-BuildOutput $buildOutputs['2019'] (Join-Path $legacyBundlePath 'Contents\Windows\2019-2020')
Copy-BuildOutput $buildOutputs['2021'] (Join-Path $legacyBundlePath 'Contents\Windows\2021-2024')
Copy-BuildOutput $buildOutputs['2025'] (Join-Path $modernBundlePath 'Contents\Windows\2025')

& (Join-Path $PSScriptRoot 'Test-Packages.ps1') -DistPath $distPath
Write-Output "Release bundles created in: $distPath"
