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

    public string DesktopPetMovementArea { get; init; } = "Taskbar";

    public CompanionSettings Normalize()
    {
        return this with
        {
            GreetingIntervalMinutes = Math.Clamp(
                GreetingIntervalMinutes,
                MinimumIntervalMinutes,
                MaximumIntervalMinutes),
            DesktopPetCount = Math.Clamp(DesktopPetCount, 1, 5),
            DesktopPetMovementArea = DesktopPetMovementArea is "Taskbar" or "FullScreen"
                ? DesktopPetMovementArea
                : "Taskbar"
        };
    }
}
