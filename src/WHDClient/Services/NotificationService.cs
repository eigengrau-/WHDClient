using System.Collections.Concurrent;
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

    /// <summary>Tickets the signed-in user has just modified themselves; their own changes must not re-notify them.</summary>
    private readonly ConcurrentDictionary<int, DateTimeOffset> _selfModified = new();

    public ObservableCollection<AppNotification> Notifications { get; } = new();

    [ObservableProperty]
    private int _unreadCount;

    public event EventHandler<int>? OpenTicketRequested;

    public NotificationService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>Records that the user themselves changed this ticket, so the next detected change is not re-notified.</summary>
    public void MarkSelfModified(int ticketId) => _selfModified[ticketId] = DateTimeOffset.UtcNow;

    public void NotifyChanges(IReadOnlyList<TicketChange> changes)
    {
        foreach (var change in changes)
        {
            if (WasSelfModified(change))
                continue;

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
                ShowToast(title, message, ticketId: change.TicketId);
        }
    }

    /// <summary>True when the change is just the user's own edit of a ticket they recently modified.</summary>
    private bool WasSelfModified(TicketChange change)
    {
        if (change.Kind != TicketChangeKind.MyTicketUpdated) return false;
        if (!_selfModified.TryRemove(change.TicketId, out var at)) return false;
        // The poll that surfaces the change runs up to one interval after the edit; allow that
        // plus a small margin. Outside the window a change is treated as someone else's.
        var grace = TimeSpan.FromSeconds(Math.Max(_settings.Settings.PollIntervalSeconds * 2, 120));
        return DateTimeOffset.UtcNow - at <= grace;
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
            ShowToast(notification.Title, notification.Message, url: notification.Url);
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

    private static void ShowToast(string title, string message, string? url = null, int? ticketId = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);
            // Clicking the toast fires ToastNotificationManagerCompat.OnActivated (App.OnStartup)
            // with these arguments; "url" opens a link in the browser, "ticketId" opens the ticket.
            if (!string.IsNullOrEmpty(url))
                builder.AddArgument("url", url);
            if (ticketId.HasValue)
                builder.AddArgument("ticketId", ticketId.Value.ToString());
            builder.Show();
        }
        catch
        {
            // Toast unavailable (e.g. focus assist / older OS) — in-app feed still has it.
        }
    }

    /// <summary>Dismisses a single notification (clicked = handled); the rest of the feed is untouched.</summary>
    public void Dismiss(AppNotification notification)
    {
        if (Notifications.Remove(notification) && UnreadCount > 0)
            UnreadCount--;
    }

    /// <summary>Removes every notification from the feed.</summary>
    public void ClearAll()
    {
        Notifications.Clear();
        UnreadCount = 0;
    }

    public void RequestOpenTicket(int ticketId)
    {
        OpenTicketRequested?.Invoke(this, ticketId);
    }
}
