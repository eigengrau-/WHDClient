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
                ShowToast(title, message, change.TicketId);
        }
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

    private static void ShowToast(string title, string message, int ticketId)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
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
