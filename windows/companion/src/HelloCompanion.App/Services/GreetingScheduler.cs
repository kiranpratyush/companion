namespace HelloCompanion.App.Services;

public sealed class GreetingScheduler : IDisposable
{
    private readonly Func<Task> _greetAsync;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private TimeSpan _interval;
    private bool _enabled;
    private DateTimeOffset? _nextGreetingAt;

    public GreetingScheduler(Func<Task> greetAsync)
    {
        _greetAsync = greetAsync;
    }

    public event EventHandler? StateChanged;

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }
    }

    public DateTimeOffset? NextGreetingAt
    {
        get
        {
            lock (_gate)
            {
                return _nextGreetingAt;
            }
        }
    }

    public void Apply(bool enabled, TimeSpan interval)
    {
        CancellationTokenSource? oldCancellation;

        lock (_gate)
        {
            oldCancellation = _cancellation;
            _cancellation = new CancellationTokenSource();
            _enabled = enabled;
            _interval = interval;
            _nextGreetingAt = enabled ? DateTimeOffset.Now.Add(interval) : null;
        }

        oldCancellation?.Cancel();
        oldCancellation?.Dispose();

        if (enabled)
        {
            _ = RunAsync(_cancellation.Token);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            cancellation = _cancellation;
            _cancellation = null;
            _enabled = false;
            _nextGreetingAt = null;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan delay;

            lock (_gate)
            {
                delay = _nextGreetingAt.HasValue
                    ? _nextGreetingAt.Value - DateTimeOffset.Now
                    : _interval;
            }

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
                await _greetAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // A failed notification must not stop future greetings.
            }

            lock (_gate)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Schedule from now so resume never replays missed greetings.
                _nextGreetingAt = DateTimeOffset.Now.Add(_interval);
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
