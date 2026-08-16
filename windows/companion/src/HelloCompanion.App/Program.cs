using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace HelloCompanion.App;

public static class Program
{
    private const string InstanceMutexName = "Local\\HelloCompanion.Main.Mutex";
    private const string ActivationEventName = "Local\\HelloCompanion.Main.Activate";
    private static DispatcherQueue? _dispatcherQueue;
    private static EventWaitHandle? _activationEvent;

    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        using Mutex instanceMutex = new(false, InstanceMutexName);
        EventWaitHandle activationEvent = new(false, EventResetMode.AutoReset, ActivationEventName);
        bool ownsInstance;

        try
        {
            ownsInstance = instanceMutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsInstance = true;
        }

        if (!ownsInstance)
        {
            activationEvent.Set();
            activationEvent.Dispose();
            return 0;
        }

        _activationEvent = activationEvent;
        _ = Task.Run(WatchForActivation);

        Application.Start(initializationCallbackParams =>
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(_dispatcherQueue));
            _ = new App();
        });

        _activationEvent.Dispose();
        _activationEvent = null;
        instanceMutex.ReleaseMutex();
        return 0;
    }

    private static void WatchForActivation()
    {
        while (_activationEvent is not null)
        {
            try
            {
                _activationEvent.WaitOne();
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    if (Application.Current is App app)
                    {
                        app.ShowMainWindow();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }
}
