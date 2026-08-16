namespace HelloCompanion.App.Models;

public sealed record CompanionSettings
{
    public const int MinimumIntervalMinutes = 1;
    public const int MaximumIntervalMinutes = 24 * 60;
    public const int DefaultIntervalMinutes = 15;

    public bool GreetingsEnabled { get; init; } = true;

    public int GreetingIntervalMinutes { get; init; } = DefaultIntervalMinutes;

    public bool DesktopPetsEnabled { get; init; } = true;

    public int DesktopPetCount { get; init; } = 2;

    public string[] SelectedPetIds { get; init; } = [];

    public string DesktopPetMovementArea { get; init; } = "Taskbar";

    public bool SleepModeEnabled { get; init; }

    public string[]? EnabledAmbientAnimations { get; init; }

    public bool RoamingEnabled { get; init; } = true;

    public CompanionSettings Normalize()
    {
        return this with
        {
            GreetingIntervalMinutes = Math.Clamp(
                GreetingIntervalMinutes,
                MinimumIntervalMinutes,
                MaximumIntervalMinutes),
            DesktopPetCount = Math.Clamp(DesktopPetCount, 1, 5),
            SelectedPetIds = (SelectedPetIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray(),
            EnabledAmbientAnimations = EnabledAmbientAnimations?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray(),
            DesktopPetMovementArea = DesktopPetMovementArea is "Taskbar" or "FullScreen"
                ? DesktopPetMovementArea
                : "Taskbar"
        };
    }
}
