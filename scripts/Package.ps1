[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$StationeersPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'LogicFilterBridge.csproj'
$project = [xml](Get-Content -LiteralPath $projectFile -Raw -Encoding UTF8)
$version = [string]$project.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not read Version from LogicFilterBridge.csproj.'
}

$buildArguments = @('build', $projectFile, '--configuration', $Configuration)
if (-not [string]::IsNullOrWhiteSpace($StationeersPath)) {
    $buildArguments += "-p:StationeersPath=$StationeersPath"
}

& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$pluginPath = Join-Path $projectRoot 'LogicFilterBridge.dll'
if (-not (Test-Path -LiteralPath $pluginPath -PathType Leaf)) {
    throw "Build succeeded but $pluginPath was not created."
}

$artifactsDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
$archivePath = Join-Path $artifactsDirectory "LogicFilterBridge-$version.zip"

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("LogicFilterBridge-package-" + [guid]::NewGuid().ToString('N'))
$payloadRoot = Join-Path $temporaryRoot 'LogicFilterBridge'
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'About') -Destination $payloadRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $projectRoot 'GameData') -Destination $payloadRoot -Recurse
    Copy-Item -LiteralPath $pluginPath -Destination $payloadRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $payloadRoot

    $licensePath = Join-Path $projectRoot 'LICENSE'
    if (Test-Path -LiteralPath $licensePath -PathType Leaf) {
        Copy-Item -LiteralPath $licensePath -Destination $payloadRoot
    }

    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath
    }

    Compress-Archive -LiteralPath $payloadRoot -DestinationPath $archivePath -CompressionLevel Optimal
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTemporaryRoot.StartsWith($resolvedSystemTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemporaryRoot).StartsWith('LogicFilterBridge-package-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Created $archivePath"
