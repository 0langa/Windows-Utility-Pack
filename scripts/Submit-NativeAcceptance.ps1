[CmdletBinding()]
param([string]$Filter = '', [switch]$PublishApp, [switch]$Installer, [switch]$LegacyMigration)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($Filter -and $Filter -notmatch '^[a-z0-9,-]+$') { throw 'Invalid filter.' }
if ($Installer -and $Filter) { throw 'Installer lifecycle cannot be combined with a tool filter.' }
if ($LegacyMigration -and ($Filter -or $Installer)) { throw 'Legacy migration must run separately.' }
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
if ($PublishApp) {
    & dotnet publish (Join-Path $root 'src/WindowsUtilityPack') -c Release -r win-x64 --self-contained true -o (Join-Path $root 'output/revival/native-app') *> (Join-Path $root 'output/revival/native-publish.log')
    if ($LASTEXITCODE) { throw 'App publish failed. See output/revival/native-publish.log.' }
}
& dotnet publish (Join-Path $root 'tests/WindowsUtilityPack.NativeTests') -c Release -r win-x64 --self-contained true -o (Join-Path $root 'output/revival/native-runner') *> (Join-Path $root 'output/revival/native-runner-publish.log')
if ($LASTEXITCODE) { throw 'Native runner publish failed. See output/revival/native-runner-publish.log.' }
$request = Join-Path $root 'output/revival/native-results/request.json'
[IO.File]::WriteAllText($request + '.tmp', (@{ Id=[Guid]::NewGuid().ToString(); Filter=$Filter; Kind=$(if ($Installer) { 'installer' } elseif ($LegacyMigration) { 'legacy' } else { 'tools' }) } | ConvertTo-Json))
Move-Item -LiteralPath ($request + '.tmp') -Destination $request -Force
