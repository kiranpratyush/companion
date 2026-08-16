namespace HelloCompanion.App.Services;

/// <summary>
/// Supplies text for pet speech bubbles. A backend-backed implementation can replace
/// this provider later without changing the animation or speech-bubble code.
/// </summary>
public interface IPetMessageProvider
{
    string? GetMessage(PetMessageRequest request, Random random);
}

public sealed record PetMessageRequest(
    string PetId,
    string PetName,
    string Activity,
    IReadOnlyList<string> CharacterMessages);

public sealed class LocalPetMessageProvider : IPetMessageProvider
{
    private static readonly string[] RoamingMessages =
    [
        "Just stretching my legs!",
        "I wonder what you're working on.",
        "Don't forget to take a tiny break.",
        "I'm keeping you company.",
        "You’ve got this!"
    ];

    private static readonly string[] QuietMessages =
    [
        "I am feeling sleepy...",
        "I'll rest here for a while.",
        "Shh... quiet time.",
        "I'll keep you company from here."
    ];

    public string? GetMessage(PetMessageRequest request, Random random)
    {
        if (request.CharacterMessages.Count > 0)
        {
            return request.CharacterMessages[random.Next(request.CharacterMessages.Count)];
        }

        return request.Activity.ToLowerInvariant() switch
        {
            "sleep" => "I am feeling sleepy...",
            "sleep-mode" => QuietMessages[random.Next(QuietMessages.Length)],
            "roam" => RoamingMessages[random.Next(RoamingMessages.Length)],
            _ => null
        };
    }
}
