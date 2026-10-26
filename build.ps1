[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src'
$resourceRoot = Join-Path $sourceRoot 'resources'
$deliveryRoot = Join-Path $projectRoot 'outputs'
$iconFile = Join-Path $resourceRoot 'nover.ico'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('nover-build-' + [Guid]::NewGuid().ToString('N'))
$releaseArchiveTemporary = Join-Path ([IO.Path]::GetTempPath()) ('nover-release-' + [Guid]::NewGuid().ToString('N') + '.zip')

function Assert-FileExists {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file not found: $Path"
    }
}

Assert-FileExists $iconFile

$programSource = Join-Path $sourceRoot 'Program.cs'
Assert-FileExists $programSource
$versionMatch = [regex]::Match(
    (Get-Content -LiteralPath $programSource -Raw),
    'ToolVersion\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Could not read ToolVersion from $programSource"
}
$toolVersion = $versionMatch.Groups[1].Value
$releaseArchiveName = "Nover-$toolVersion-windows.zip"
$releaseArchivePath = Join-Path $projectRoot $releaseArchiveName
$checksumPath = $releaseArchivePath + '.sha256'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "C# compiler not found at $compiler"
}

try {
    # 在仓库外编译，避免生成 bin。
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $deliveryRoot -Force | Out-Null

    $sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | Sort-Object Name
    if (-not $sources) {
        throw "No C# sources found in $sourceRoot"
    }

    $compiledExe = Join-Path $temporaryRoot 'nover.exe'
    $arguments = @(
        '/nologo',
        '/target:exe',
        '/platform:anycpu',
        '/optimize+',
        '/warn:4',
        '/warnaserror+',
        '/r:System.dll',
        '/r:System.Core.dll',
        '/r:System.Web.Extensions.dll',
        '/r:System.IO.Compression.dll',
        '/r:System.IO.Compression.FileSystem.dll',
        "/win32icon:$iconFile",
        "/out:$compiledExe"
    ) + ($sources | ForEach-Object { $_.FullName })

    & $compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed with exit code $LASTEXITCODE"
    }

    Assert-FileExists $compiledExe
    Assert-FileExists (Join-Path $resourceRoot 'nover.config')
    Assert-FileExists (Join-Path $resourceRoot 'lang\default\messages.ini')
    Assert-FileExists (Join-Path $resourceRoot 'lang\default\help.txt')
    Assert-FileExists (Join-Path $resourceRoot 'lang\zh-CN\messages.ini')
    Assert-FileExists (Join-Path $resourceRoot 'lang\zh-CN\help.txt')

    Copy-Item -LiteralPath (Join-Path $resourceRoot 'nover.config') -Destination $temporaryRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $temporaryRoot
    Copy-Item -LiteralPath (Join-Path $resourceRoot 'lang') -Destination $temporaryRoot -Recurse

    $resolvedDelivery = [IO.Path]::GetFullPath($deliveryRoot)
    $deliveryPrefix = $resolvedDelivery.TrimEnd('\') + '\'
    # 当前构建直接管理的文件。
    $managedFiles = @(
        'nover.exe',
        'nover.config',
        'LICENSE'
    )
    foreach ($name in $managedFiles) {
        $path = Join-Path $resolvedDelivery $name
        if ([IO.File]::Exists($path)) {
            [IO.File]::Delete($path)
        }
    }

    $deliveryLang = Join-Path $resolvedDelivery 'lang'
    if ([IO.Directory]::Exists($deliveryLang)) {
        # 递归删除前确认 lang 位于 outputs 内。
        $resolvedLang = [IO.Path]::GetFullPath($deliveryLang)
        if (-not ($resolvedLang + '\').StartsWith($deliveryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to replace language directory outside outputs: $resolvedLang"
        }
        [IO.Directory]::Delete($resolvedLang, $true)
    }

    foreach ($file in (Get-ChildItem -LiteralPath $temporaryRoot -File)) {
        Copy-Item -LiteralPath $file.FullName -Destination $resolvedDelivery
    }
    Copy-Item -LiteralPath (Join-Path $temporaryRoot 'lang') -Destination $resolvedDelivery -Recurse

    $nodeShim = Join-Path $resolvedDelivery 'node.exe'
    if ([IO.File]::Exists($nodeShim)) {
        # 已初始化目录中同步更新 node shim。
        Copy-Item -LiteralPath $compiledExe -Destination $nodeShim -Force
    }

    # 只打包临时目录，避免带入本机运行状态。
    Compress-Archive `
        -Path (Join-Path $temporaryRoot '*') `
        -DestinationPath $releaseArchiveTemporary `
        -CompressionLevel Optimal `
        -Force
    [IO.File]::Copy($releaseArchiveTemporary, $releaseArchivePath, $true)
    $archiveHash = (Get-FileHash -LiteralPath $releaseArchivePath -Algorithm SHA256).Hash
    $checksumText = $archiveHash + '  ' + $releaseArchiveName + [Environment]::NewLine
    [IO.File]::WriteAllText($checksumPath, $checksumText, [Text.Encoding]::ASCII)

    Write-Host "Built executable: $resolvedDelivery\nover.exe"
    Write-Host "Delivery folder:  $resolvedDelivery"
    Write-Host "Release archive:  $releaseArchivePath"
    Write-Host "SHA-256 file:     $checksumPath"
}
finally {
    if ([IO.File]::Exists($releaseArchiveTemporary)) {
        [IO.File]::Delete($releaseArchiveTemporary)
    }
    if ([IO.Directory]::Exists($temporaryRoot)) {
        [IO.Directory]::Delete($temporaryRoot, $true)
    }
}
