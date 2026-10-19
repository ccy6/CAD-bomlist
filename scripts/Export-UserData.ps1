[CmdletBinding()]
param(
    [string] $AppDataPath = [Environment]::GetFolderPath('ApplicationData')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $AppDataPath 'BomCadPlugin\component-rules.json'
$destination = Join-Path $repositoryRoot 'userdata\BomCadPlugin\component-rules.json'
$library = Get-Content -LiteralPath $source -Raw -Encoding UTF8 | ConvertFrom-Json
if (@($library.productSystems).Count -eq 0 -or @($library.rules).Count -eq 0) {
    throw 'No product systems or rules were found. Nothing was exported.'
}
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $destination -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
    throw 'Rule library changed during export. Close AutoCAD and retry.'
}
Write-Host "Exported $(@($library.productSystems).Count) systems and $(@($library.rules).Count) rules to: $destination"
Write-Host 'Commit and push this file to synchronize the snapshot with GitHub.'
