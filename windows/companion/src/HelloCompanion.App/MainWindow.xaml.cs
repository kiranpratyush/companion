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
    private string _statusTitle = "Your pets are active";
    private string _statusDetail = string.Empty;

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

    private void Runtime_StateChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        LoadSettingsIntoControls();
        RefreshStatus();
    });

    private void LoadSettingsIntoControls()
    {
        CompanionSettings settings = _runtime.Settings;
        DesktopPetsToggle.IsOn = settings.DesktopPetsEnabled;
        PetMovementAreaComboBox.SelectedIndex = settings.DesktopPetMovementArea == "FullScreen" ? 1 : 0;
        SleepModeToggle.IsOn = settings.SleepModeEnabled;

        AvailablePetsList.ItemsSource = _runtime.AvailablePets;
        AvailablePetsList.SelectedItems.Clear();
        HashSet<string> selectedIds = new(_runtime.SelectedPetIds, StringComparer.OrdinalIgnoreCase);
        foreach (PetSelectionOption pet in _runtime.AvailablePets)
        {
            if (selectedIds.Contains(pet.Id))
            {
                AvailablePetsList.SelectedItems.Add(pet);
            }
        }

        AvailableAnimationsList.ItemsSource = _runtime.AvailableAmbientAnimations;
        AvailableAnimationsList.SelectedItems.Clear();
        HashSet<string>? enabledAnimations = settings.EnabledAmbientAnimations is null
            ? null
            : new HashSet<string>(settings.EnabledAmbientAnimations, StringComparer.OrdinalIgnoreCase);
        foreach (AnimationSelectionOption animation in _runtime.AvailableAmbientAnimations)
        {
            bool selected = string.Equals(animation.Name, "roam", StringComparison.OrdinalIgnoreCase)
                ? settings.RoamingEnabled
                : enabledAnimations is null || enabledAnimations.Contains(animation.Name);
            if (selected)
            {
                AvailableAnimationsList.SelectedItems.Add(animation);
            }
        }
    }

    private void RefreshStatus()
    {
        bool enabled = _runtime.Settings.DesktopPetsEnabled;
        if (!enabled)
        {
            StatusTitle = "Your pets are hidden";
            StatusDetail = "Turn on desktop pets below to see them again.";
        }
        else if (_runtime.IsSleepModeEnabled)
        {
            StatusTitle = "Your pets are sleeping";
            StatusDetail = "Turn off sleep mode when you want them to roam again.";
        }
        else
        {
            StatusTitle = "Your pets are active";
            StatusDetail = _runtime.Settings.RoamingEnabled
                ? "They will speak occasionally while roaming and when they feel sleepy."
                : "Roaming is off, so they will stay in place and use only the selected behaviors.";
        }
        StatusInfoBar.Message = _runtime.LastError ?? string.Empty;
        StatusInfoBar.IsOpen = !string.IsNullOrWhiteSpace(_runtime.LastError);
        LoadedPetsText.Text = $"Loaded characters: {_runtime.AvailablePetsSummary}";
    }

    private async void SavePets_Click(object sender, RoutedEventArgs e)
    {
        string movementArea = PetMovementAreaComboBox.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? "Taskbar"
            : "Taskbar";
        string[] selectedPetIds = AvailablePetsList.SelectedItems
            .OfType<PetSelectionOption>()
            .Select(pet => pet.Id)
            .Take(5)
            .ToArray();
        string[] enabledBehaviors = AvailableAnimationsList.SelectedItems
            .OfType<AnimationSelectionOption>()
            .Select(animation => animation.Name)
            .ToArray();
        bool roamingEnabled = enabledBehaviors.Contains("roam", StringComparer.OrdinalIgnoreCase);
        string[] enabledAmbientAnimations = enabledBehaviors
            .Where(name => !string.Equals(name, "roam", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        await _runtime.ApplyPetsAsync(
            DesktopPetsToggle.IsOn && selectedPetIds.Length > 0,
            Math.Max(1, selectedPetIds.Length),
            movementArea,
            SleepModeToggle.IsOn,
            selectedPetIds,
            enabledAmbientAnimations,
            roamingEnabled);
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
        LoadSettingsIntoControls();
        RefreshStatus();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
