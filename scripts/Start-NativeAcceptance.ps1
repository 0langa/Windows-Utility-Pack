[CmdletBinding()]
param([string]$Filter = '', [switch]$Watch)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$result = Join-Path $root 'output/revival/native-results'
New-Item -ItemType Directory -Path $result -Force | Out-Null
$escape = { param($value) [Security.SecurityElement]::Escape($value) }
$folders = @(
    @{ Host='output/revival/native-app'; Guest='C:\WupApp'; ReadOnly='true' },
    @{ Host='output/revival/native-runner'; Guest='C:\WupNative'; ReadOnly='true' },
    @{ Host='scripts'; Guest='C:\WupScripts'; ReadOnly='true' },
    @{ Host='output/installer'; Guest='C:\WupInstallers'; ReadOnly='true' },
    @{ Host='output/revival/native-results'; Guest='C:\WupResults'; ReadOnly='false' }
)
$mappings = foreach ($folder in $folders) {
    $hostPath = & $escape (Join-Path $root $folder.Host)
    "<MappedFolder><HostFolder>$hostPath</HostFolder><SandboxFolder>$($folder.Guest)</SandboxFolder><ReadOnly>$($folder.ReadOnly)</ReadOnly></MappedFolder>"
}
if ($Filter -and $Filter -notmatch '^[a-z0-9,-]+$') { throw 'Filter accepts only comma-separated tool keys.' }
$command = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\WupScripts\Run-NativeAcceptance.ps1'
if ($Filter) { $command += " -Filter $Filter" }
if ($Watch) { $command += ' -Watch' }
$xml = '<Configuration><VGpu>Disable</VGpu><Networking>Enable</Networking><MemoryInMB>6144</MemoryInMB><ClipboardRedirection>Disable</ClipboardRedirection><PrinterRedirection>Disable</PrinterRedirection><AudioInput>Disable</AudioInput><VideoInput>Disable</VideoInput><MappedFolders>' + ($mappings -join '') + '</MappedFolders><LogonCommand><Command>' + (& $escape $command) + '</Command></LogonCommand></Configuration>'
$config = Join-Path $root 'output/revival/native-acceptance.wsb'
[IO.File]::WriteAllText($config,$xml)
# Interactive window is intentional: the user can observe and stop native tests.
Start-Process -FilePath $config
