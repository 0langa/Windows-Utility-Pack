namespace WindowsUtilityPack.Services.TextConversion;
internal static class StaThreadInvoker

/// <summary>
/// Runs WPF-dependent conversion work (for example RTF parsing) on a dedicated
/// STA thread so the main UI thread stays responsive and the code remains testable.
/// </summary>
{
    public static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        var taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    taskCompletionSource.TrySetCanceled(cancellationToken);
                    return;
                }

                taskCompletionSource.TrySetResult(work());
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "WindowsUtilityPack-STA-Worker",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => taskCompletionSource.TrySetCanceled(cancellationToken));
        }

        return taskCompletionSource.Task;
    }
}