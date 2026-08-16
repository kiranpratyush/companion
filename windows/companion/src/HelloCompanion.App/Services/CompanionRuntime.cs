using System.Diagnostics;
using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

public sealed class CompanionRuntime : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DesktopPetManager _petManager;
    private CompanionSettings _settings = new();
    private string? _lastError;
    private bool _disposed;

    public CompanionRuntime(
        SettingsService settingsService,
        DesktopPetManager petManager)
    {
        _settingsService = settingsService;
        _petManager = petManager;
        _petManager.StateChanged += OnPetManagerStateChanged;
    }

    public event EventHandler? StateChanged;
    public CompanionSettings Settings => _settings;
    public string? LastError => _lastError ?? _petManager.LastError;
    public string CustomPetsDirectory => _petManager.CustomPetsDirectory;
    public bool IsSleepModeEnabled => _petManager.IsSleepModeEnabled;
    public string AvailablePetsSummary => _petManager.AvailablePetNames.Count == 0
        ? "No characters loaded"
        : string.Join(", ", _petManager.AvailablePetNames);
    public IReadOnlyList<PetSelectionOption> AvailablePets => _petManager.AvailablePets;
    public IReadOnlyList<string> SelectedPetIds => _settings.SelectedPetIds;
    public IReadOnlyList<AnimationSelectionOption> AvailableAmbientAnimations
        => _petManager.AvailableAmbientAnimations;

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        ApplyToPetManager();

        if (_settings.SelectedPetIds.Length == 0 && _petManager.SelectedPetIds.Count > 0)
        {
            _settings = _settings with { SelectedPetIds = _petManager.SelectedPetIds.ToArray() };
            await SaveSettingsAsync();
        }
    }

    public async Task ApplyPetsAsync(
        bool enabled,
        int petCount,
        string movementArea,
        bool sleepModeEnabled,
        IReadOnlyCollection<string> selectedPetIds,
        IReadOnlyCollection<string>? enabledAmbientAnimations,
        bool roamingEnabled)
    {
        _settings = (_settings with
        {
            DesktopPetsEnabled = enabled,
            DesktopPetCount = petCount,
            DesktopPetMovementArea = movementArea,
            SleepModeEnabled = sleepModeEnabled,
            SelectedPetIds = selectedPetIds.ToArray(),
            EnabledAmbientAnimations = enabledAmbientAnimations?.ToArray(),
            RoamingEnabled = roamingEnabled
        }).Normalize();

        await SaveSettingsAsync();
        ApplyToPetManager();
    }

    public Task TogglePetsAsync()
    {
        IReadOnlyCollection<string> selectedPetIds = _settings.SelectedPetIds.Length > 0
            ? _settings.SelectedPetIds
            : _petManager.SelectedPetIds;
        return ApplyPetsAsync(
            !_settings.DesktopPetsEnabled,
            _settings.DesktopPetCount,
            _settings.DesktopPetMovementArea,
            _settings.SleepModeEnabled,
            selectedPetIds,
            _settings.EnabledAmbientAnimations,
            _settings.RoamingEnabled);
    }

    public async Task TogglePetSelectionAsync(string petId)
    {
        HashSet<string> selected = new(_settings.SelectedPetIds, StringComparer.OrdinalIgnoreCase);
        if (!selected.Remove(petId))
        {
            if (selected.Count >= 5)
            {
                _lastError = "You can show up to five pets at once.";
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            selected.Add(petId);
        }

        await ApplyPetsAsync(
            selected.Count > 0,
            _settings.DesktopPetCount,
            _settings.DesktopPetMovementArea,
            _settings.SleepModeEnabled,
            selected,
            _settings.EnabledAmbientAnimations,
            _settings.RoamingEnabled);
    }

    public void ReloadPets() => ApplyToPetManager();

    public void OpenCustomPetsDirectory()
    {
        Directory.CreateDirectory(CustomPetsDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CustomPetsDirectory}\"")
        {
            UseShellExecute = true
        });
    }


    public void ReportTrayUnavailable()
    {
        _lastError = "The notification-area icon could not be created, so keep this window open to control the app.";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyToPetManager() => _petManager.Apply(
        _settings.DesktopPetsEnabled,
        _settings.DesktopPetCount,
        _settings.DesktopPetMovementArea,
        _settings.SleepModeEnabled,
        _settings.SelectedPetIds,
        _settings.EnabledAmbientAnimations,
        _settings.RoamingEnabled);

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

    private void OnPetManagerStateChanged(object? sender, EventArgs e) => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _petManager.StateChanged -= OnPetManagerStateChanged;
        _petManager.Dispose();
    }
}
