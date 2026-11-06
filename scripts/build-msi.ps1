param(
    [string]$PublishDir = "$PSScriptRoot\..\..\publish\ZenithPower",
    [string]$MsiOutputDir = "$PSScriptRoot\..\..\publish\MSI"
)

Write-Host "Building MSI Installer for ZenithPower..." -ForegroundColor Cyan

if (-not (Test-Path "$PublishDir\ZenithPower.UI.exe")) {
    Write-Host "Running publish-portable.ps1 first..." -ForegroundColor Yellow
    & "$PSScriptRoot\publish-portable.ps1" -OutputDir $PublishDir
}

if (-not (Test-Path $MsiOutputDir)) {
    New-Item -ItemType Directory -Path $MsiOutputDir -Force | Out-Null
}

$wixTool = Get-Command "wix" -ErrorAction SilentlyContinue
$candleTool = Get-Command "candle.exe" -ErrorAction SilentlyContinue

if ($wixTool) {
    Write-Host "Detected modern .NET WiX tool. Building MSI..." -ForegroundColor Green
    & wix build "$PSScriptRoot\..\packaging\ZenithPower.wxs" -d PublishDir=$PublishDir -o "$MsiOutputDir\ZenithPowerSetup.msi"
} elseif ($candleTool) {
    Write-Host "Detected WiX v3 toolset. Compiling and linking..." -ForegroundColor Green
    & candle.exe "$PSScriptRoot\..\packaging\ZenithPower.wxs" -dPublishDir=$PublishDir -out "$MsiOutputDir\ZenithPower.wixobj"
    & light.exe "$MsiOutputDir\ZenithPower.wixobj" -out "$MsiOutputDir\ZenithPowerSetup.msi"
} else {
    Write-Host "WiX Toolset not found in PATH." -ForegroundColor Yellow
    Write-Host "To produce the MSI installer, install WiX with: 'dotnet tool install --global wix'" -ForegroundColor Yellow
    Write-Host "The single-file portable executable is already available at: $PublishDir\ZenithPower.UI.exe" -ForegroundColor Green
}
