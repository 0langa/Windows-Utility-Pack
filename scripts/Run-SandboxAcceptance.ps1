# Runs only in Windows Sandbox. The host maps the app and this script read-only.
$ErrorActionPreference = 'Stop'
$results = 'C:\WupResults'
if ($env:USERNAME -ne 'WDAGUtilityAccount' -or -not (Test-Path -LiteralPath $results)) {
    throw 'This script may run only as WDAGUtilityAccount inside the configured Windows Sandbox.'
}
$report = [ordered]@{ StartedUtc = [DateTime]::UtcNow.ToString('O'); Completed = $false }
try {
    $report.User = $env:USERNAME
    $report.Windows = [Environment]::OSVersion.VersionString
    $report.Administrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $report.DotnetOnPath = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
    $report.InstalledSdkDirectories = @(Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\sdk' -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    $localRoot = 'C:\WupAcceptance'
    New-Item -ItemType Directory -Path "$localRoot\App","$localRoot\Profile" -Force | Out-Null
    Copy-Item -Path 'C:\WupApp\*' -Destination "$localRoot\App" -Recurse
    [IO.File]::WriteAllText("$localRoot\Profile\settings.json", '{"RememberWindowPosition":false,"HotkeysEnabled":false,"TrayModeEnabled":false,"CloseToTray":false,"ClipboardMonitoringEnabled":false,"ClipboardMonitoringConsentAccepted":false}')
    $app = Start-Process -FilePath "$localRoot\App\WindowsUtilityPack.exe" -ArgumentList '--profile-directory',"$localRoot\Profile" -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $app.Refresh()
        if ($app.HasExited) { throw "The app exited during startup with code $($app.ExitCode)." }
    } while ($app.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($app.MainWindowHandle -eq 0) { throw 'No main window appeared within 45 seconds.' }
    $report.MainWindowTitle = $app.MainWindowTitle
    $report.ProcessId = $app.Id
    $report.RuntimeModules = @($app.Modules | Where-Object { $_.ModuleName -in @('coreclr.dll','hostfxr.dll','hostpolicy.dll') } | Select-Object ModuleName,FileName)
    Start-Sleep -Seconds 10
    $app.Refresh()
    if ($app.HasExited) { throw "The app exited after its window opened: $($app.ExitCode)." }
    $report.Completed = $true
    if (Test-Path -LiteralPath 'C:\WupProbe\WindowsUtilityPack.UiSmoke.exe') {
        New-Item -ItemType Directory -Path "$localRoot\Probe" -Force | Out-Null
        Copy-Item -Path 'C:\WupProbe\*' -Destination "$localRoot\Probe" -Recurse
        $probe = Start-Process -FilePath "$localRoot\Probe\WindowsUtilityPack.UiSmoke.exe" -ArgumentList 'C:\WupResults\services.json','--sandbox-services' -WindowStyle Hidden -PassThru
        if (-not $probe.WaitForExit(330000)) { $probe.Kill(); throw 'Sandbox service probe timed out.' }
        $report.ServiceProbeExitCode = $probe.ExitCode
        if ($probe.ExitCode -ne 0) { throw "Sandbox service probe exited $($probe.ExitCode). See services.json." }
    }
} catch {
    $report.Completed = $false
    $report.Error = $_.Exception.ToString()
} finally {
    $report.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$results\clean-startup.json" -Encoding UTF8
}
