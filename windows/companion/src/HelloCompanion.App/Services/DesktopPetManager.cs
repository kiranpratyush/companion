using System.Diagnostics;
using System.Runtime.InteropServices;
using HelloCompanion.App.Models;
using Microsoft.UI.Dispatching;

namespace HelloCompanion.App.Services;

public sealed class DesktopPetManager : IDisposable
{
    private readonly PetCatalog _catalog;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _animationTimer;
    private readonly List<IPetActor> _pets = [];
    private readonly SemaphoreSlim _reminderGate = new(1, 1);
    private CancellationTokenSource _petCancellation = new();
    private long _lastTick;
    private int _nextReminderPet;
    private bool _disposed;

    public DesktopPetManager(PetCatalog catalog, DispatcherQueue dispatcherQueue)
    {
        _catalog = catalog;
        _dispatcherQueue = dispatcherQueue;
        _animationTimer = dispatcherQueue.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(33);
        _animationTimer.IsRepeating = true;
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    public event EventHandler? StateChanged;

    public string? LastError { get; private set; }

    public IReadOnlyList<string> AvailablePetNames { get; private set; } = [];

    public string CustomPetsDirectory => _catalog.CustomPetsDirectory;

    public void Apply(bool enabled, int petCount, string movementArea)
    {
        StopPets();

        IReadOnlyList<PetDefinition> definitions = _catalog.Load();
        AvailablePetNames = definitions.Select(definition => definition.DisplayName).ToArray();

        if (!enabled)
        {
            LastError = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (definitions.Count == 0)
        {
            LastError = "No valid pet characters were found. Add a pet package or reinstall the built-in assets.";
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            PetMovementArea area = movementArea == "FullScreen"
                ? PetMovementArea.FullScreen
                : PetMovementArea.Taskbar;

            int count = Math.Clamp(petCount, 1, 5);
            for (int index = 0; index < count; index++)
            {
                PetDefinition definition = definitions[index % definitions.Count];
                _pets.Add(new ConfiguredPetActor(definition, area, index, count));
            }

            LastError = null;
            _lastTick = Stopwatch.GetTimestamp();
            _animationTimer.Start();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException)
        {
            StopPets();
            LastError = $"The desktop pets could not start: {exception.Message}";
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> HandleReminderAsync(ReminderContext reminder)
    {
        await _reminderGate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return false;
            }

            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken cancellationToken = _petCancellation.Token;

            if (!_dispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (_pets.Count == 0)
                        {
                            completion.TrySetResult(false);
                            return;
                        }

                        IPetActor pet = SelectReminderPet();
                        await pet.HandleReminderAsync(reminder, cancellationToken);
                        completion.TrySetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        completion.TrySetResult(false);
                    }
                    catch (Exception exception) when (exception is IOException or ExternalException)
                    {
                        LastError = $"A pet could not show the reminder: {exception.Message}";
                        completion.TrySetResult(false);
                        StateChanged?.Invoke(this, EventArgs.Empty);
                    }
                }))
            {
                return false;
            }

            return await completion.Task;
        }
        finally
        {
            _reminderGate.Release();
        }
    }

    private IPetActor SelectReminderPet()
    {
        for (int offset = 0; offset < _pets.Count; offset++)
        {
            int index = (_nextReminderPet + offset) % _pets.Count;
            if (!_pets[index].IsBusy)
            {
                _nextReminderPet = (index + 1) % _pets.Count;
                return _pets[index];
            }
        }

        IPetActor fallback = _pets[_nextReminderPet % _pets.Count];
        _nextReminderPet = (_nextReminderPet + 1) % _pets.Count;
        return fallback;
    }

    private void AnimationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = Math.Clamp(
            (double)(now - _lastTick) / Stopwatch.Frequency,
            0,
            0.1);
        _lastTick = now;

        try
        {
            foreach (IPetActor pet in _pets)
            {
                pet.Update(elapsedSeconds);
            }
        }
        catch (ExternalException exception)
        {
            StopPets();
            LastError = $"A desktop pet stopped because Windows could not draw it: {exception.Message}";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StopPets()
    {
        _animationTimer.Stop();
        _petCancellation.Cancel();
        _petCancellation.Dispose();
        _petCancellation = new CancellationTokenSource();
        foreach (IPetActor pet in _pets)
        {
            pet.Dispose();
        }

        _pets.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _animationTimer.Tick -= AnimationTimer_Tick;
        StopPets();
        _petCancellation.Dispose();
    }
}

internal enum PetMovementArea
{
    Taskbar,
    FullScreen
}

internal readonly record struct DesktopBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

internal readonly record struct TaskbarPlacement(DesktopBounds Bounds, TaskbarEdge Edge);

internal enum TaskbarEdge : uint
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

internal static class DesktopGeometry
{
    private const uint GetTaskbarPosition = 0x00000005;

    public static DesktopBounds GetVirtualScreen()
    {
        int left = GetSystemMetrics(76);
        int top = GetSystemMetrics(77);
        int width = GetSystemMetrics(78);
        int height = GetSystemMetrics(79);
        return new DesktopBounds(left, top, left + width, top + height);
    }

    public static TaskbarPlacement GetTaskbarPlacement(DesktopBounds fallbackScreen)
    {
        AppBarData data = new()
        {
            Size = (uint)Marshal.SizeOf<AppBarData>()
        };

        if (SHAppBarMessage(GetTaskbarPosition, ref data) != 0)
        {
            return new TaskbarPlacement(
                new DesktopBounds(data.Rectangle.Left, data.Rectangle.Top, data.Rectangle.Right, data.Rectangle.Bottom),
                data.Edge);
        }

        return new TaskbarPlacement(
            new DesktopBounds(fallbackScreen.Left, fallbackScreen.Bottom - 48, fallbackScreen.Right, fallbackScreen.Bottom),
            TaskbarEdge.Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint CallbackMessage;
        public TaskbarEdge Edge;
        public NativeRectangle Rectangle;
        public IntPtr Parameter;
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
}
