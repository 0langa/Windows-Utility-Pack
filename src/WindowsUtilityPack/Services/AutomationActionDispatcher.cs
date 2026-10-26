using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>Executes a claimed automation action through injected application boundaries.</summary>
public sealed class AutomationActionDispatcher(
    IProcessExplorerService processes,
    Func<string, CancellationToken, Task> openTool)
{
    public async Task DispatchAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (rule.ActionType)
        {
            case AutomationActionType.ShowNotification:
                return;
            case AutomationActionType.LaunchTool:
                ArgumentException.ThrowIfNullOrWhiteSpace(rule.ActionTarget);
                await openTool(rule.ActionTarget.Trim(), cancellationToken).ConfigureAwait(false);
                return;
            case AutomationActionType.RunCleanup:
                await openTool("storage-master", cancellationToken).ConfigureAwait(false);
                return;
            case AutomationActionType.KillProcess:
                ArgumentException.ThrowIfNullOrWhiteSpace(rule.ActionTarget);
                var target = rule.ActionTarget.Trim();
                if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target = target[..^4];
                if (target.IndexOfAny(['\\', '/', '*', '?']) >= 0)
                    throw new ArgumentException("Process targets must be an exact executable name.", nameof(rule));
                var matches = (await processes.GetProcessesAsync(null, cancellationToken).ConfigureAwait(false))
                    .Where(process => string.Equals(process.Name, target, StringComparison.OrdinalIgnoreCase)
                        && process.StartTimeLocal.HasValue && process.ProcessId != Environment.ProcessId && process.ProcessId > 4)
                    .ToArray();
                foreach (var process in matches)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!await processes.TryTerminateAsync(process, cancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException($"Process {process.Name} ({process.ProcessId}) changed or could not be terminated.");
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), "Unsupported automation action.");
        }
    }
}
