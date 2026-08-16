using System.Text.Json.Serialization;

namespace HelloCompanion.App.Models;

public sealed class PetDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string[] Frames { get; init; } = [];

    public Dictionary<string, string[]> Animations { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> AnimationFrameDurations { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> Messages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string[] NonLoopingAnimations { get; init; } = [];

    public PetAmbientBehavior[] AmbientBehaviors { get; init; } = [];

    public PetBehaviorAction[] ReminderBehavior { get; init; } = [];

    public PetBehaviorAction[] ClickBehavior { get; init; } = [];

    public int FrameDurationMilliseconds { get; init; } = 160;

    public int SpriteWidth { get; init; } = 112;

    public int SpriteHeight { get; init; } = 96;

    public double SpeedPixelsPerSecond { get; init; } = 72;

    public bool PixelArt { get; init; }

    [JsonIgnore]
    public string AssetDirectory { get; init; } = string.Empty;
}

public sealed class PetBehaviorAction
{
    public string Action { get; init; } = string.Empty;

    public string? Animation { get; init; }

    public double Seconds { get; init; }
}

public sealed class PetAmbientBehavior
{
    public string Animation { get; init; } = string.Empty;

    public double Weight { get; init; } = 1;

    public double MinimumSeconds { get; init; } = 3;

    public double MaximumSeconds { get; init; } = 6;
}
