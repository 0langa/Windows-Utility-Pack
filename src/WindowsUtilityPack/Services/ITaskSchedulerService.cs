using System.Diagnostics;
using System.Text;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides read/run operations for Windows Task Scheduler tasks.
/// </summary>
public interface ITaskSchedulerService
{
    Task<IReadOnlyList<ScheduledTaskRow>> GetTasksAsync(string? query = null, CancellationToken cancellationToken = default);

    Task<bool> RunTaskAsync(string taskName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Structured Windows PowerShell task queries and exact-name schtasks execution.
/// </summary>
public sealed class TaskSchedulerService : ITaskSchedulerService
{
    private readonly IProcessRunner _runner;

    public TaskSchedulerService()
        : this(new ProcessRunner())
    {
    }

    internal TaskSchedulerService(IProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<IReadOnlyList<ScheduledTaskRow>> GetTasksAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
            $rows = @(Get-ScheduledTask | ForEach-Object {
                $task = $_
                try {
                    $info = $task | Get-ScheduledTaskInfo
                    $next = if ($null -ne $info.NextRunTime) { $info.NextRunTime.ToString('O') } else { '' }
                    $last = if ($null -ne $info.LastRunTime) { $info.LastRunTime.ToString('O') } else { '' }
                    $result = [string]$info.LastTaskResult
                } catch { $next = ''; $last = ''; $result = 'Details unavailable: ' + $_.Exception.Message }
                [pscustomobject]@{
                    TaskName = $task.TaskPath + $task.TaskName
                    Status = [string]$task.State
                    NextRunTime = $next; LastRunTime = $last; LastResult = $result; Author = [string]$task.Author
                }
            })
            ConvertTo-Json -InputObject $rows -Compress -Depth 3
            """;
        var psi = new ProcessStartInfo
        {
            FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) psi.ArgumentList.Add(argument);
        var result = await _runner.RunAsync(psi, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StdErr)
                ? "Unable to query scheduled tasks."
                : result.StdErr.Trim());
        }

        var rows = System.Text.Json.JsonSerializer.Deserialize<List<ScheduledTaskRow>>(result.StdOut)
            ?? throw new InvalidOperationException("The scheduler returned no structured result.");
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(t => t.TaskName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return rows
            .OrderBy(t => t.TaskName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> RunTaskAsync(string taskName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return false;
        }

        if (taskName.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new ArgumentException("Invalid task name.", nameof(taskName));
        var psi = CreateStartInfo(string.Empty);
        psi.ArgumentList.Add("/Run"); psi.ArgumentList.Add("/TN"); psi.ArgumentList.Add(taskName);
        var result = await _runner.RunAsync(psi, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    internal static List<ScheduledTaskRow> ParseTasksFromCsv(string csv)
    {
        var lines = (csv ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return [];
        }

        var headers = ParseCsvLine(lines[0]);
        var headerMap = headers
            .Select((name, index) => (name, index))
            .ToDictionary(t => t.name, t => t.index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ScheduledTaskRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count == 0)
            {
                continue;
            }

            rows.Add(new ScheduledTaskRow
            {
                TaskName = GetField("TaskName", headerMap, fields),
                Status = GetField("Status", headerMap, fields),
                NextRunTime = GetField("Next Run Time", headerMap, fields),
                LastRunTime = GetField("Last Run Time", headerMap, fields),
                LastResult = GetField("Last Result", headerMap, fields),
                Author = GetField("Author", headerMap, fields),
            });
        }

        return rows;
    }

    private static ProcessStartInfo CreateStartInfo(string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
    }

    private static string GetField(string name, IReadOnlyDictionary<string, int> map, IReadOnlyList<string> fields)
    {
        return map.TryGetValue(name, out var index) && index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line is null)
        {
            return result;
        }

        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
