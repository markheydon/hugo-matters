namespace HugoMatters.Web.Components.Shared;

/// <summary>
/// Client-side stepped progress for long single-request actions (connect, session start, preview).
/// </summary>
public sealed class SteppedProgressRunner : IAsyncDisposable
{
    private readonly string[] _steps;
    private readonly Func<Task> _onChanged;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    /// <summary>Creates a runner that cycles through <paramref name="steps"/> while work runs.</summary>
    public SteppedProgressRunner(string[] steps, Func<Task> onChanged)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(onChanged);
        if (steps.Length == 0)
        {
            throw new ArgumentException("At least one progress step is required.", nameof(steps));
        }

        _steps = steps;
        _onChanged = onChanged;
        Message = steps[0];
    }

    /// <summary>Current status line.</summary>
    public string Message { get; private set; }

    /// <summary>Approximate percent complete (0–100).</summary>
    public int Percent { get; private set; }

    /// <summary>
    /// Runs <paramref name="work"/> while advancing stepped progress, then shows the completed message briefly.
    /// </summary>
    public async Task<T> RunAsync<T>(
        Func<Task<T>> work,
        string completedMessage,
        int stepDelayMs = 900,
        int completedHoldMs = 250)
    {
        ArgumentNullException.ThrowIfNull(work);

        await CancelLoopAsync().ConfigureAwait(false);
        _cts = new CancellationTokenSource();
        Percent = 8;
        Message = _steps[0];
        await _onChanged().ConfigureAwait(false);
        _loopTask = RunLoopAsync(stepDelayMs, _cts.Token);

        try
        {
            var result = await work().ConfigureAwait(false);
            Percent = 100;
            Message = completedMessage;
            await _onChanged().ConfigureAwait(false);
            if (completedHoldMs > 0)
            {
                await Task.Delay(completedHoldMs).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            await CancelLoopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> while advancing stepped progress.
    /// </summary>
    public Task RunAsync(
        Func<Task> work,
        string completedMessage,
        int stepDelayMs = 900,
        int completedHoldMs = 250) =>
        RunAsync(
            async () =>
            {
                await work().ConfigureAwait(false);
                return true;
            },
            completedMessage,
            stepDelayMs,
            completedHoldMs);

    private async Task RunLoopAsync(int stepDelayMs, CancellationToken cancellationToken)
    {
        var step = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            Message = _steps[Math.Min(step, _steps.Length - 1)];
            Percent = Math.Min(92, 8 + (step * 18));
            await _onChanged().ConfigureAwait(false);

            try
            {
                await Task.Delay(stepDelayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            step++;
        }
    }

    private async Task CancelLoopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling the progress loop.
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CancelLoopAsync().ConfigureAwait(false);
    }
}
