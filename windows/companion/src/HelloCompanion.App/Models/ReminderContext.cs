namespace HelloCompanion.App.Models;

public sealed record ReminderContext(
    string Title,
    string Message,
    TimeSpan DisplayDuration);
