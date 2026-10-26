[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Compiler,
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version = '1.0.0',
    [string]$OutputDirectory = 'output/installer',
    [string]$PublishDirectory = '',
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory, $root)
$publish = if ($PublishDirectory) { [IO.Path]::GetFullPath($PublishDirectory, $root) } else { Join-Path $output "payload-$Version" }
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (-not (Test-Path -LiteralPath $Compiler -PathType Leaf)) { throw 'Supply the absolute path to Inno Setup 7 ISCC.exe.' }
if (-not $SkipPublish) {
    $env:PROCESSOR_ARCHITECTURE = 'AMD64'
    & dotnet publish (Join-Path $root 'src/WindowsUtilityPack/WindowsUtilityPack.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $publish *> (Join-Path $output "publish-$Version.log")
    if ($LASTEXITCODE) { throw "Publish failed. See $output\publish-$Version.log" }
}
foreach ($required in @('WindowsUtilityPack.exe','WindowsUtilityPack.dll','coreclr.dll','PresentationFramework.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Self-contained payload is missing $required" }
}
& $Compiler "/DAppVersion=$Version" "/DPublishDir=$publish" "/DInstallerOutput=$output" (Join-Path $root 'packaging/WindowsUtilityPack.iss') *> (Join-Path $output "installer-$Version.log")
if ($LASTEXITCODE) { throw "Installer build failed. See $output\installer-$Version.log" }
Get-FileHash -LiteralPath (Join-Path $output "WindowsUtilityPack-$Version-win-x64-setup.exe") | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output "sha256-$Version.json")
