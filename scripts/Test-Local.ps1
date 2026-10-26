[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$DisableIPv6
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$results = Join-Path $repository 'output\validation'
[IO.Directory]::CreateDirectory($results) | Out-Null
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

function Invoke-ValidationStep {
    param([string]$Name, [string[]]$Arguments, [int]$TimeoutSeconds = 240)

    $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    if ($DisableIPv6) { $start.Environment['DOTNET_SYSTEM_NET_DISABLEIPV6'] = '1' }
    # Some automation hosts omit this Windows variable; the SDK workload probe needs it.
    # Restore it only for this owned child, without changing the user's environment.
    if (-not $start.Environment['PROCESSOR_ARCHITECTURE']) {
        $architecture = [Environment]::GetEnvironmentVariable('PROCESSOR_ARCHITECTURE', 'Machine')
        if ($architecture) { $start.Environment['PROCESSOR_ARCHITECTURE'] = $architecture }
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            # This handle belongs to the child started above, including its child processes.
            $process.Kill($true)
            $process.WaitForExit()
        }
        $log = Join-Path $results "$Name.log"
        [IO.File]::WriteAllText($log, $stdout.GetAwaiter().GetResult() + [Environment]::NewLine + $stderr.GetAwaiter().GetResult())
        if ($timedOut) { throw "$Name timed out after $TimeoutSeconds seconds. See $log" }
        if ($process.ExitCode -ne 0) {
            throw "$Name failed (exit $($process.ExitCode)). See $log"
        }
    }
    finally { $process.Dispose() }
}

Invoke-ValidationStep 'sdk-info' @('--info')
Invoke-ValidationStep 'restore' @('restore', 'WindowsUtilityPack.sln', '--verbosity', 'quiet')
Invoke-ValidationStep 'build' @('build', 'WindowsUtilityPack.sln', '-c', $Configuration, '--no-restore', '--verbosity', 'quiet')
Invoke-ValidationStep 'tests' @('test', 'tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj', '-c', $Configuration,
    '--no-build', '--no-restore', '--verbosity', 'quiet', '--collect:XPlat Code Coverage',
    '--logger', 'trx;LogFileName=unit-tests.trx', '--results-directory', 'output/validation/tests')
Invoke-ValidationStep 'ui-smoke' @("tests/WindowsUtilityPack.UiSmoke/bin/$Configuration/net10.0-windows/WindowsUtilityPack.UiSmoke.dll",
    'output/validation/ui-smoke.json')
