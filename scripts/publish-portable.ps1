param(
    [string]$OutputDir = "$PSScriptRoot\..\..\publish\ZenithPower"
)

Write-Host "Publishing ZenithPower as Portable Single-File Executable..." -ForegroundColor Cyan

$projectPath = "$PSScriptRoot\..\src\ZenithPower.UI\ZenithPower.UI.csproj"

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDir

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully published to: $OutputDir" -ForegroundColor Green
    Get-ChildItem $OutputDir
} else {
    Write-Host "Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
