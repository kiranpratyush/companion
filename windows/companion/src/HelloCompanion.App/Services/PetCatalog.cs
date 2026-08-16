using System.Text.Json;
using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

public sealed class PetCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PetCatalog()
    {
        CustomPetsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelloCompanion",
            "Pets");
    }

    public string CustomPetsDirectory { get; }

    public IReadOnlyList<PetDefinition> Load()
    {
        Directory.CreateDirectory(CustomPetsDirectory);
        List<PetDefinition> definitions = [];

        LoadFromRoot(Path.Combine(AppContext.BaseDirectory, "Assets", "Pets"), definitions);
        LoadFromRoot(CustomPetsDirectory, definitions);

        return definitions
            .GroupBy(definition => definition.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
    }

    private static void LoadFromRoot(string rootDirectory, List<PetDefinition> definitions)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (string manifestPath in Directory.EnumerateFiles(rootDirectory, "pet.json", SearchOption.AllDirectories))
        {
            try
            {
                PetDefinition? source = JsonSerializer.Deserialize<PetDefinition>(
                    File.ReadAllText(manifestPath),
                    SerializerOptions);
                string assetDirectory = Path.GetDirectoryName(manifestPath)!;

                string[] baseFrames = source?.Frames ?? [];
                Dictionary<string, string[]> animations = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string name, string[]? frames) in source?.Animations ?? new())
                {
                    if (!string.IsNullOrWhiteSpace(name) && frames is not null)
                    {
                        animations[name.Trim()] = frames;
                    }
                }

                Dictionary<string, int> animationFrameDurations = source?.AnimationFrameDurations ?? new(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> normalizedFrameDurations = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string name, int duration) in animationFrameDurations)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        normalizedFrameDurations[name.Trim()] = Math.Clamp(duration, 50, 2000);
                    }
                }

                Dictionary<string, string[]> messages = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string activity, string[]? lines) in source?.Messages ?? new())
                {
                    if (string.IsNullOrWhiteSpace(activity) || lines is null)
                    {
                        continue;
                    }

                    string[] normalizedLines = lines
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .Where(line => line.Length <= 240)
                        .Distinct(StringComparer.Ordinal)
                        .Take(30)
                        .ToArray();
                    if (normalizedLines.Length > 0 && messages.Count < 20)
                    {
                        messages[activity.Trim()] = normalizedLines;
                    }
                }

                PetAmbientBehavior[] ambientBehaviors = source?.AmbientBehaviors ?? [];
                PetBehaviorAction[] reminderBehavior = source?.ReminderBehavior ?? [];
                PetBehaviorAction[] clickBehavior = source?.ClickBehavior ?? [];

                if (source is null ||
                    string.IsNullOrWhiteSpace(source.Id) ||
                    string.IsNullOrWhiteSpace(source.DisplayName) ||
                    (baseFrames.Length == 0 && animations.Count == 0))
                {
                    continue;
                }

                string normalizedDirectory = Path.GetFullPath(assetDirectory) + Path.DirectorySeparatorChar;
                IEnumerable<string> allFrames = baseFrames.Concat(
                    animations.Values.Where(frames => frames is not null).SelectMany(frames => frames));
                bool framesAreSafe = allFrames.All(frame =>
                {
                    if (string.IsNullOrWhiteSpace(frame))
                    {
                        return false;
                    }

                    string fullPath = Path.GetFullPath(Path.Combine(assetDirectory, frame));
                    return fullPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase) &&
                           File.Exists(fullPath) &&
                           string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase);
                });

                if (!framesAreSafe)
                {
                    continue;
                }

                HashSet<string> availableAnimations = new(animations.Keys, StringComparer.OrdinalIgnoreCase)
                {
                    "roam"
                };

                definitions.Add(new PetDefinition
                {
                    Id = source.Id.Trim(),
                    DisplayName = source.DisplayName.Trim(),
                    Frames = baseFrames,
                    Animations = new Dictionary<string, string[]>(animations, StringComparer.OrdinalIgnoreCase),
                    AnimationFrameDurations = normalizedFrameDurations,
                    Messages = messages,
                    NonLoopingAnimations = (source.NonLoopingAnimations ?? [])
                        .Where(name => !string.IsNullOrWhiteSpace(name) && availableAnimations.Contains(name))
                        .Select(name => name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    AmbientBehaviors = ambientBehaviors
                        .Where(behavior =>
                            behavior is not null &&
                            !string.IsNullOrWhiteSpace(behavior.Animation) &&
                            availableAnimations.Contains(behavior.Animation))
                        .Select(behavior => new PetAmbientBehavior
                        {
                            Animation = behavior.Animation.Trim(),
                            Weight = Math.Clamp(behavior.Weight, 0.1, 100),
                            MinimumSeconds = Math.Clamp(behavior.MinimumSeconds, 0.5, 120),
                            MaximumSeconds = Math.Clamp(
                                behavior.MaximumSeconds,
                                Math.Clamp(behavior.MinimumSeconds, 0.5, 120),
                                120)
                        })
                        .ToArray(),
                    ReminderBehavior = reminderBehavior.Where(action => action is not null).ToArray(),
                    ClickBehavior = clickBehavior.Where(action => action is not null).ToArray(),
                    FrameDurationMilliseconds = Math.Clamp(source.FrameDurationMilliseconds, 50, 2000),
                    SpriteWidth = Math.Clamp(source.SpriteWidth, 32, 512),
                    SpriteHeight = Math.Clamp(source.SpriteHeight, 32, 512),
                    SpeedPixelsPerSecond = Math.Clamp(source.SpeedPixelsPerSecond, 10, 500),
                    PixelArt = source.PixelArt,
                    AssetDirectory = assetDirectory
                });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // One broken custom character must not prevent other characters from loading.
            }
        }
    }
}
