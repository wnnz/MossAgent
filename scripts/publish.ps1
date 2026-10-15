[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$solution = Join-Path $repositoryRoot 'MossAgent.slnx'
$project = Join-Path $repositoryRoot 'src\MossAgent.App\MossAgent.App.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\publish'
$publishProfile = 'WindowsX64'
$outputDirectory = Join-Path $artifactRoot $Runtime
$archivePath = Join-Path $artifactRoot "MossAgent-$Version-$Runtime.zip"
$operation = [Guid]::NewGuid().ToString('N')
$stagingDirectory = Join-Path $artifactRoot ".staging-$operation"
$stagingArchive = Join-Path $artifactRoot ".staging-$operation.zip"
$backupDirectory = Join-Path $artifactRoot ".backup-$operation"
$backupArchive = Join-Path $artifactRoot ".backup-$operation.zip"

if (-not (Test-Path -LiteralPath $solution) -or -not (Test-Path -LiteralPath $project)) {
    throw '脚本必须从 MossAgent 仓库内运行。'
}

function Assert-PublishPath([string]$Path) {
    $absolutePath = [System.IO.Path]::GetFullPath($Path)
    if (-not $absolutePath.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw '发布路径必须位于 MossAgent 仓库内。'
    }
    $ancestor = $absolutePath
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw '发布路径不能包含符号链接或重解析点。'
            }
        }
        $ancestor = Split-Path -Parent $ancestor
    }
}

function Remove-PublishPath([string]$Path) {
    Assert-PublishPath $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Move-PublishPath([string]$Source, [string]$Destination) {
    Assert-PublishPath $Source
    Assert-PublishPath $Destination
    Move-Item -LiteralPath $Source -Destination $Destination
}

foreach ($path in @($artifactRoot, $outputDirectory, $archivePath, $stagingDirectory,
        $stagingArchive, $backupDirectory, $backupArchive)) {
    Assert-PublishPath $path
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

dotnet restore $project -r $Runtime "-p:PublishProfile=$publishProfile"
if ($LASTEXITCODE -ne 0) { throw '发布配置 restore 失败。' }

$backedUpDirectory = $false
$backedUpArchive = $false
$installedDirectory = $false
$installedArchive = $false
$completed = $false
try {
    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained true `
        --no-restore `
        -o $stagingDirectory `
        "-p:PublishProfile=$publishProfile" `
        "-p:Version=$Version" `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false"
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败；保留之前的发布包。' }

    Assert-PublishPath $stagingDirectory
    $executable = Join-Path $stagingDirectory 'MossAgent.App.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw '发布目录中没有生成 MossAgent.App.exe。'
    }
    $publishedFiles = @(Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File -Force)
    foreach ($file in $publishedFiles) { Assert-PublishPath $file.FullName }
    if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].FullName -ne $executable) {
        throw '单文件发布目录必须只包含 MossAgent.App.exe，不能遗漏外部依赖文件。'
    }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingDirectory, $stagingArchive,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $hash = (Get-FileHash -LiteralPath $stagingArchive -Algorithm SHA256).Hash

    if (Test-Path -LiteralPath $outputDirectory) {
        Move-PublishPath $outputDirectory $backupDirectory
        $backedUpDirectory = $true
    }
    if (Test-Path -LiteralPath $archivePath) {
        Move-PublishPath $archivePath $backupArchive
        $backedUpArchive = $true
    }
    Move-PublishPath $stagingDirectory $outputDirectory
    $installedDirectory = $true
    Move-PublishPath $stagingArchive $archivePath
    $installedArchive = $true
    $completed = $true
}
catch {
    if ($installedArchive) { Remove-PublishPath $archivePath }
    if ($installedDirectory) { Remove-PublishPath $outputDirectory }
    if ($backedUpArchive) { Move-PublishPath $backupArchive $archivePath }
    if ($backedUpDirectory) { Move-PublishPath $backupDirectory $outputDirectory }
    throw
}
finally {
    Remove-PublishPath $stagingDirectory
    Remove-PublishPath $stagingArchive
    if ($completed) {
        Remove-PublishPath $backupDirectory
        Remove-PublishPath $backupArchive
    }
}

Write-Host "Published: $outputDirectory"
Write-Host "Archive:   $archivePath"
Write-Host "SHA256:    $hash"
Write-Host "Files:     $($publishedFiles.Count) (self-extracting single-file)"
