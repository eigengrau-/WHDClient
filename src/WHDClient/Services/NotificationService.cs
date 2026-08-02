using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp.Notifications;
using WHDClient.Core.ChangeDetection;

namespace WHDClient.Services;

public class AppNotification
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public int? TicketId { get; init; }
    /// <summary>When set, clicking the notification opens this URL (e.g. an update download) instead of a ticket.</summary>
    public string? Url { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string TimeDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss");
}

/// <summary>
/// Turns ticket changes into Windows toast notifications + an in-app notification feed.
/// </summary>
public partial class NotificationService : ObservableObject
{
    private readonly SettingsService _settings;

    public ObservableCollection<AppNotification> Notifications { get; } = new();

    [ObservableProperty]
    private int _unreadCount;

    public event EventHandler<int>? OpenTicketRequested;

    public NotificationService(SettingsService settings)
    {
        _settings = settings;
    }

    public void NotifyChanges(IReadOnlyList<TicketChange> changes)
    {
        foreach (var change in changes)
        {
            var (title, message) = Describe(change);
            var notification = new AppNotification
            {
                Title = title,
                Message = message,
                TicketId = change.TicketId
            };

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Notifications.Insert(0, notification);
                while (Notifications.Count > 100) Notifications.RemoveAt(Notifications.Count - 1);
                UnreadCount++;
            });

            if (_settings.Settings.NotificationsEnabled)
                ShowToast(title, message);
        }
    }

    /// <summary>Alerts the user that a newer app version is available (in-app feed + toast).</summary>
    public void NotifyUpdateAvailable(UpdateInfo update)
    {
        var notification = new AppNotification
        {
            Title = "Update available",
            Message = $"WHD Client v{update.Version} is available — click to download.",
            Url = update.Url
        };

        Application.Current?.Dispatcher.Invoke(() =>
        {
            // Replace any earlier update notification rather than stacking them.
            for (int i = Notifications.Count - 1; i >= 0; i--)
                if (Notifications[i].Url != null) Notifications.RemoveAt(i);
            Notifications.Insert(0, notification);
            UnreadCount++;
        });

        if (_settings.Settings.NotificationsEnabled)
            ShowToast(notification.Title, notification.Message, notification.Url);
    }

    private (string Title, string Message) Describe(TicketChange c) => c.Kind switch
    {
        TicketChangeKind.AssignedToMe =>
            ("Ticket assigned to you", $"#{c.TicketId}: {c.Subject}"),
        TicketChangeKind.MyTicketUpdated =>
            ("Your ticket was updated", $"#{c.TicketId}: {c.Subject}"),
        TicketChangeKind.NewMatchingTicket =>
            ($"New ticket matching '{c.SourceName}'", $"#{c.TicketId}: {c.Subject}"),
        _ => ("Ticket change", $"#{c.TicketId}: {c.Subject}")
    };

    private static void ShowToast(string title, string message, string? url = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);
            // Clicking the toast fires ToastNotificationManagerCompat.OnActivated (App.OnStartup)
            // with these arguments; "url" means "open this link in the browser".
            if (!string.IsNullOrEmpty(url))
                builder.AddArgument("url", url);
            builder.Show();
        }
        catch
        {
            // Toast unavailable (e.g. focus assist / older OS) — in-app feed still has it.
        }
    }

    public void MarkAllRead()
    {
        UnreadCount = 0;
    }

    public void RequestOpenTicket(int ticketId)
    {
        OpenTicketRequested?.Invoke(this, ticketId);
    }
}
