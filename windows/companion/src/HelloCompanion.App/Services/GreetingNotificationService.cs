using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace HelloCompanion.App.Services;

public sealed class GreetingNotificationService
{
    public Task ShowGreetingAsync()
    {
        AppNotification notification = new AppNotificationBuilder()
            .AddText("Hello Companion")
            .AddText("Hello 👋")
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
        return Task.CompletedTask;
    }
}
