using Microsoft.UI.Xaml;
using HelloCompanion.App.Services;
using Microsoft.UI.Dispatching;

namespace HelloCompanion.App;

public partial class App : Application
{
    private MainWindow? _window;
    private CompanionRuntime? _runtime;
    private TrayIconService? _trayIcon;
    private bool _isExiting;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        PetCatalog petCatalog = new();
        DesktopPetManager petManager = new(petCatalog, DispatcherQueue.GetForCurrentThread());
        _runtime = new CompanionRuntime(new SettingsService(), petManager);
        await _runtime.InitializeAsync();
        _window = new MainWindow(_runtime);
        _window.Activate();

        try
        {
            _trayIcon = new TrayIconService();
            _trayIcon.SetPetsVisible(_runtime.Settings.DesktopPetsEnabled);
            _trayIcon.SetPetSelections(_runtime.AvailablePets, _runtime.SelectedPetIds);
            _trayIcon.OpenRequested += (_, _) => ShowMainWindow();
            _trayIcon.TogglePetsRequested += async (_, _) => await _runtime.TogglePetsAsync();
            _trayIcon.PetSelectionRequested += async petId => await _runtime.TogglePetSelectionAsync(petId);
            _trayIcon.ExitRequested += (_, _) => ExitApplication();
            _window.CloseToTrayEnabled = true;
        }
        catch
        {
            _runtime.ReportTrayUnavailable();
        }

        _runtime.StateChanged += OnRuntimeStateChanged;
        _window.ExitRequested += (_, _) => ExitApplication();
    }

    public void ShowMainWindow() => _window?.ShowAndActivate();

    private void OnRuntimeStateChanged(object? sender, EventArgs e)
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_runtime is not null)
            {
                _trayIcon?.SetPetsVisible(_runtime.Settings.DesktopPetsEnabled);
                _trayIcon?.SetPetSelections(_runtime.AvailablePets, _runtime.SelectedPetIds);
            }
        });
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _runtime?.Dispose();
        _window?.PrepareForExit();
        _window?.Close();
        Exit();
    }
}
