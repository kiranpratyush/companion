using System.Diagnostics;
using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

public sealed class CompanionRuntime : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly GreetingNotificationService _notificationService;
    private readonly GreetingScheduler _scheduler;
    private readonly DesktopPetManager _petManager;
    private CompanionSettings _settings = new();
    private string? _lastError;
    private bool _disposed;

    public CompanionRuntime(
        SettingsService settingsService,
        GreetingNotificationService notificationService,
        DesktopPetManager petManager)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _scheduler = new GreetingScheduler(ScheduledGreetingAsync);
        _petManager = petManager;
        _scheduler.StateChanged += OnSchedulerStateChanged;
        _petManager.StateChanged += OnPetManagerStateChanged;
    }

    public event EventHandler? StateChanged;
    public CompanionSettings Settings => _settings;
    public DateTimeOffset? NextGreetingAt => _scheduler.NextGreetingAt;
    public string? LastError => _lastError ?? _petManager.LastError;
    public string CustomPetsDirectory => _petManager.CustomPetsDirectory;
    public string AvailablePetsSummary => _petManager.AvailablePetNames.Count == 0
        ? "No characters loaded"
        : string.Join(", ", _petManager.AvailablePetNames);

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        ApplyToScheduler();
        ApplyToPetManager();
    }

    public async Task ApplyAsync(bool enabled, int intervalMinutes)
    {
        _settings = (_settings with
        {
            GreetingsEnabled = enabled,
            GreetingIntervalMinutes = intervalMinutes
        }).Normalize();

        await SaveSettingsAsync();

        ApplyToScheduler();
    }

    public async Task ApplyPetsAsync(bool enabled, int petCount, string movementArea)
    {
        _settings = (_settings with
        {
            DesktopPetsEnabled = enabled,
            DesktopPetCount = petCount,
            DesktopPetMovementArea = movementArea
        }).Normalize();

        await SaveSettingsAsync();
        ApplyToPetManager();
    }

    public Task TogglePetsAsync() => ApplyPetsAsync(
        !_settings.DesktopPetsEnabled,
        _settings.DesktopPetCount,
        _settings.DesktopPetMovementArea);

    public void ReloadPets() => ApplyToPetManager();

    public void OpenCustomPetsDirectory()
    {
        Directory.CreateDirectory(CustomPetsDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CustomPetsDirectory}\"")
        {
            UseShellExecute = true
        });
    }

    public Task TogglePauseAsync() => ApplyAsync(!_settings.GreetingsEnabled, _settings.GreetingIntervalMinutes);

    public async Task SayHelloNowAsync()
    {
        try
        {
            await DeliverGreetingAsync();
            _lastError = null;
        }
        catch
        {
            _lastError = "Windows could not show the greeting. Check the app's notification settings.";
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportTrayUnavailable()
    {
        _lastError = "The notification-area icon could not be created, so keep this window open to control the app.";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ScheduledGreetingAsync()
    {
        try
        {
            await DeliverGreetingAsync();
            _lastError = null;
        }
        catch
        {
            _lastError = "Windows could not show the scheduled greeting. Check the app's notification settings.";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task DeliverGreetingAsync()
    {
        ReminderContext reminder = new(
            "Hello Companion",
            "Hello 👋",
            TimeSpan.FromSeconds(5));

        bool handledByPet = await _petManager.HandleReminderAsync(reminder);
        if (!handledByPet)
        {
            await _notificationService.ShowGreetingAsync();
        }
    }

    private void ApplyToScheduler() => _scheduler.Apply(_settings.GreetingsEnabled, TimeSpan.FromMinutes(_settings.GreetingIntervalMinutes));
    private void ApplyToPetManager() => _petManager.Apply(
        _settings.DesktopPetsEnabled,
        _settings.DesktopPetCount,
        _settings.DesktopPetMovementArea);

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
            _lastError = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _lastError = "The settings could not be saved. Your current choices will work until the app exits.";
        }
    }

    private void OnSchedulerStateChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);
    private void OnPetManagerStateChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scheduler.StateChanged -= OnSchedulerStateChanged;
        _petManager.StateChanged -= OnPetManagerStateChanged;
        _scheduler.Dispose();
        _petManager.Dispose();
    }
}
