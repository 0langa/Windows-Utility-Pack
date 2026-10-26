param([string]$Filter = '', [switch]$Watch)
$ErrorActionPreference = 'Stop'
if ($env:USERNAME -ne 'WDAGUtilityAccount' -or -not (Test-Path -LiteralPath 'C:\WupResults')) {
    throw 'Native system acceptance may run only in the configured Windows Sandbox.'
}
$lastRequest = ''
do {
if ($Watch) {
    $requestPath = 'C:\WupResults\request.json'
    do {
        Start-Sleep -Milliseconds 500
        $request = if (Test-Path -LiteralPath $requestPath) { Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json } else { $null }
    } while (-not $request -or $request.Id -eq $lastRequest)
    $requestId = [Guid]::Parse($request.Id).ToString()
    $lastRequest = $requestId
    $Filter = [string]$request.Filter
    if ($Filter -and $Filter -notmatch '^[a-z0-9,-]+$') { throw 'Invalid tool-key filter.' }
} else { $requestId = [Guid]::NewGuid().ToString() }
$report = [ordered]@{ Id = $requestId; StartedUtc = [DateTime]::UtcNow.ToString('O'); Completed = $false }
$report | ConvertTo-Json | Set-Content -LiteralPath 'C:\WupResults\native-run.json' -Encoding UTF8
try {
    $runRoot = "C:\WupAcceptance\$requestId"
    $runResults = "C:\WupResults\$requestId"
    New-Item -ItemType Directory -Path "$runRoot\App","$runRoot\Runner","$runRoot\Work",$runResults -Force | Out-Null
    Copy-Item -Path 'C:\WupApp\*' -Destination "$runRoot\App" -Recurse
    Copy-Item -Path 'C:\WupNative\*' -Destination "$runRoot\Runner" -Recurse
    $arguments = @("$runRoot\App\WindowsUtilityPack.exe","$runRoot\Work","$runResults\native.json")
    if ($Filter) { $arguments += @('--filter',$Filter) }
    if ($Watch -and $request.Kind -eq 'installer') { $arguments = @('C:\WupInstallers',"$runRoot\Work","$runResults\installer.json",'--installer') }
    if ($Watch -and $request.Kind -eq 'legacy') {
        Copy-Item -LiteralPath 'C:\WupInstallers\WindowsUtilityPack-0.1.0-alpha.exe' -Destination "$runRoot\legacy.exe"
        $arguments = @("$runRoot\App\WindowsUtilityPack.exe","$runRoot\Work","$runResults\legacy.json",'--legacy',"$runRoot\legacy.exe")
    }
    $runner = Start-Process -FilePath "$runRoot\Runner\WindowsUtilityPack.NativeTests.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardError "$runResults\native-errors.log" -RedirectStandardOutput "$runResults\native-output.log"
    $null = $runner.Handle
    if (-not $runner.WaitForExit(1200000)) { $runner.Kill(); throw 'Native suite exceeded 20 minutes.' }
    $report.ExitCode = $runner.ExitCode
    if ($runner.ExitCode -ne 0) { throw 'Native suite failed. See native.json and the failing control tree.' }
    $report.Completed = $true
} catch { $report.Error = $_.Exception.ToString() }
finally {
    $report.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath 'C:\WupResults\native-run.json' -Encoding UTF8
}
} while ($Watch)
