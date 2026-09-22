[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$Version = '0.1.0',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'MossAgent.slnx'
$project = Join-Path $repositoryRoot 'src\MossAgent.App\MossAgent.App.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\publish'
$outputDirectory = Join-Path $artifactRoot $Runtime
$archivePath = Join-Path $artifactRoot "MossAgent-$Version-$Runtime.zip"

if (-not (Test-Path -LiteralPath $solution) -or -not (Test-Path -LiteralPath $project)) {
    throw '脚本必须从 MossAgent 仓库内运行。'
}

dotnet restore $solution -r $Runtime
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet restore 失败。'
}

if (-not $SkipTests) {
    dotnet test $solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Release 测试失败，已停止发布。'
    }
}

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    --no-restore `
    -o $outputDirectory `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish 失败。'
}

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $outputDirectory '*') -DestinationPath $archivePath
$executable = Join-Path $outputDirectory 'MossAgent.App.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw '发布目录中没有生成 MossAgent.App.exe。'
}

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
Write-Host "Published: $outputDirectory"
Write-Host "Archive:   $archivePath"
Write-Host "SHA256:    $hash"
