using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

public interface IPetActor : IDisposable
{
    string Id { get; }

    string DisplayName { get; }

    bool IsBusy { get; }

    void Update(double elapsedSeconds);

    void SetSleepMode(bool enabled);

    Task HandleReminderAsync(ReminderContext reminder, CancellationToken cancellationToken = default);

    Task HandleClickAsync(CancellationToken cancellationToken = default);

    void Pause();

    void Resume();
}
