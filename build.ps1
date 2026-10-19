param(
    [string]$Configuration = 'Release',
    [switch]$Installer
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\DeviceBatteryTray\DeviceBatteryTray.csproj'
$publishDir = Join-Path $root 'publish'

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

Write-Host "发布完成: $publishDir"

if ($Installer) {
    $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if (-not $iscc) {
        throw '未找到 Inno Setup (ISCC.exe)。请先安装 Inno Setup 6。'
    }

    & $iscc.Source (Join-Path $root 'installer\DeviceBatteryTray.iss')
    Write-Host "安装包完成: $(Join-Path $root 'installer\Output\DeviceBatteryTray-Setup.exe')"
}
