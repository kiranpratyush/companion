using System.ComponentModel;
using System.Runtime.CompilerServices;
using HelloCompanion.App.Models;
using HelloCompanion.App.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HelloCompanion.App;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly CompanionRuntime _runtime;
    private readonly AppWindow _appWindow;
    private bool _allowClose;
    private string _statusTitle = "Greetings are on";
    private string _statusDetail = string.Empty;
    private string _pauseResumeLabel = "Pause greetings";

    public MainWindow(CompanionRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();

        IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(760, 840));

        Closed += MainWindow_Closed;
        _runtime.StateChanged += Runtime_StateChanged;
        LoadSettingsIntoControls();
        RefreshStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ExitRequested;

    public bool CloseToTrayEnabled { get; set; }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetField(ref _statusTitle, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetField(ref _statusDetail, value);
    }

    public string PauseResumeLabel
    {
        get => _pauseResumeLabel;
        private set => SetField(ref _pauseResumeLabel, value);
    }

    public void ShowAndActivate()
    {
        _appWindow.Show();
        Activate();
    }

    public void PrepareForExit()
    {
        _allowClose = true;
        _runtime.StateChanged -= Runtime_StateChanged;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!_allowClose && CloseToTrayEnabled)
        {
            args.Handled = true;
            _appWindow.Hide();
        }
    }

    private void Runtime_StateChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshStatus);

    private void LoadSettingsIntoControls()
    {
        CompanionSettings settings = _runtime.Settings;
        GreetingsToggle.IsOn = settings.GreetingsEnabled;
        DesktopPetsToggle.IsOn = settings.DesktopPetsEnabled;
        PetCountNumberBox.Value = settings.DesktopPetCount;
        PetMovementAreaComboBox.SelectedIndex = settings.DesktopPetMovementArea == "FullScreen" ? 1 : 0;

        if (settings.GreetingIntervalMinutes >= 60 && settings.GreetingIntervalMinutes % 60 == 0)
        {
            IntervalNumberBox.Value = settings.GreetingIntervalMinutes / 60d;
            IntervalUnitComboBox.SelectedIndex = 1;
        }
        else
        {
            IntervalNumberBox.Value = settings.GreetingIntervalMinutes;
            IntervalUnitComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshStatus()
    {
        bool enabled = _runtime.Settings.GreetingsEnabled;
        StatusTitle = enabled ? "Greetings are on" : "Greetings are paused";
        PauseResumeLabel = enabled ? "Pause greetings" : "Resume greetings";
        StatusDetail = enabled && _runtime.NextGreetingAt is DateTimeOffset nextGreeting
            ? $"Next hello at {nextGreeting:hh:mm tt}."
            : "No greeting is currently scheduled.";
        StatusInfoBar.Message = _runtime.LastError ?? string.Empty;
        StatusInfoBar.IsOpen = !string.IsNullOrWhiteSpace(_runtime.LastError);
        LoadedPetsText.Text = $"Loaded characters: {_runtime.AvailablePetsSummary}";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _runtime.ApplyAsync(GreetingsToggle.IsOn, GetIntervalMinutes());
        LoadSettingsIntoControls();
        RefreshStatus();
    }

    private async void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        await _runtime.TogglePauseAsync();
        LoadSettingsIntoControls();
        RefreshStatus();
    }

    private async void SayHelloNow_Click(object sender, RoutedEventArgs e)
    {
        await _runtime.SayHelloNowAsync();
        RefreshStatus();
    }

    private async void SavePets_Click(object sender, RoutedEventArgs e)
    {
        int petCount = double.IsNaN(PetCountNumberBox.Value)
            ? 1
            : Math.Clamp((int)Math.Round(PetCountNumberBox.Value), 1, 5);
        string movementArea = PetMovementAreaComboBox.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? "Taskbar"
            : "Taskbar";

        await _runtime.ApplyPetsAsync(DesktopPetsToggle.IsOn, petCount, movementArea);
        LoadSettingsIntoControls();
        RefreshStatus();
    }

    private void OpenPetsFolder_Click(object sender, RoutedEventArgs e)
    {
        _runtime.OpenCustomPetsDirectory();
    }

    private void ReloadPets_Click(object sender, RoutedEventArgs e)
    {
        _runtime.ReloadPets();
        RefreshStatus();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private int GetIntervalMinutes()
    {
        double value = double.IsNaN(IntervalNumberBox.Value) ? 1 : IntervalNumberBox.Value;
        bool hours = IntervalUnitComboBox.SelectedItem is ComboBoxItem item &&
                     string.Equals(item.Tag?.ToString(), "Hours", StringComparison.Ordinal);
        return Math.Clamp((int)Math.Round(hours ? value * 60 : value),
            CompanionSettings.MinimumIntervalMinutes,
            CompanionSettings.MaximumIntervalMinutes);
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
