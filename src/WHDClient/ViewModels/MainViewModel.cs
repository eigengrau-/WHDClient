using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WHDClient.Core.ChangeDetection;
using WHDClient.Services;
using WHDClient.Views;

namespace WHDClient.ViewModels;

public abstract partial class TabViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _header = "";

    /// <summary>Pack URI of the icon shown left of the tab title; null for no icon.</summary>
    public string? IconSource { get; init; }

    public virtual bool IsClosable => false;

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

public partial class MainViewModel : ObservableObject
{
    private readonly WhdSessionContext _session;
    private readonly SettingsService _settings;
    private readonly PollingService _polling;
    private readonly NotificationService _notifications;
    private readonly UpdateService _updates;
    private readonly GridLayoutService _gridLayout;

    public ObservableCollection<TabViewModelBase> Tabs { get; } = new();

    [ObservableProperty]
    private TabViewModelBase? _selectedTab;

    [ObservableProperty]
    private string _statusText = "";

    public NotificationService Notifications => _notifications;
    public string UserDisplay => $"Signed in as {_session.CurrentTech?.DisplayName ?? "?"}";

    private MyTicketsViewModel? _myTickets;
    private SearchViewModel? _search;
    private QueueViewModel? _queue;
    private SettingsViewModel? _settingsPage;
    private BookmarksViewModel? _bookmarks;

    public MainViewModel(WhdSessionContext session, SettingsService settings,
        PollingService polling, NotificationService notifications, UpdateService updates,
        GridLayoutService gridLayout)
    {
        _session = session;
        _settings = settings;
        _polling = polling;
        _notifications = notifications;
        _updates = updates;
        _gridLayout = gridLayout;

        // Only My Tickets is a permanent tab; the other pages open on demand from the sidebar.
        _myTickets = new MyTicketsViewModel(_session, _settings, OpenTicket);
        Tabs.Add(_myTickets);
        SelectedTab = _myTickets;

        _polling.ChangesDetected += OnChangesDetected;
        _polling.PollFailed += (_, msg) => StatusText = $"Poll failed: {msg}";
        _polling.Start();

        _notifications.OpenTicketRequested += (_, id) => OpenTicket(id);

        StatusText = $"Connected to {_session.ServerUrl}";

        // Silent update check on startup; alerts via the notification feed when newer exists.
        _ = CheckForUpdatesAsync();

        // Restore the tabs that were open when the app was last closed.
        foreach (var key in _settings.Settings.OpenTabs)
        {
            if (key.Equals("newticket", StringComparison.OrdinalIgnoreCase))
                OpenNewTicketCommand.Execute(null);
            else if (key.StartsWith("ticket:", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(key[7..], out var tid))
                OpenTicket(tid);
            else
                ShowPage(key);
        }
        var selected = Tabs.FirstOrDefault(t => TabKey(t) == _settings.Settings.SelectedTab);
        if (selected != null) SelectedTab = selected;

        // Dev/test hook: WHD_START_PAGE=mine|search|queue|settings|newticket|ticket:<id>
        var startPage = Environment.GetEnvironmentVariable("WHD_START_PAGE");
        if (!string.IsNullOrEmpty(startPage))
        {
            if (startPage.StartsWith("ticket:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(startPage[7..], out var tid))
                OpenTicket(tid);
            else if (startPage.Equals("newticket", StringComparison.OrdinalIgnoreCase))
                OpenNewTicketCommand.Execute(null);
            else
                ShowPageCommand.Execute(startPage.ToLowerInvariant());
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        var (update, _) = await _updates.CheckForUpdateAsync();
        if (update != null)
            _notifications.NotifyUpdateAvailable(update);
    }

    private void OnChangesDetected(object? sender, IReadOnlyList<TicketChange> changes)
    {
        _notifications.NotifyChanges(changes);
        // Refresh open views so the user sees the change immediately.
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            if (_myTickets != null) await _myTickets.RefreshAsync();
            StatusText = $"Last update {DateTime.Now:HH:mm:ss}";
        });
    }

    public void OpenTicket(int ticketId)
    {
        var existing = Tabs.OfType<TicketTabViewModel>().FirstOrDefault(t => t.TicketId == ticketId);
        if (existing != null)
        {
            SelectedTab = existing;
            _ = existing.RefreshAsync();
            return;
        }
        var tab = new TicketTabViewModel(_session, _settings, _notifications, ticketId, OnBookmarkChanged);
        tab.CloseRequested += (_, _) => CloseTab(tab);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>Keys of all restorable tabs in display order; My Tickets is permanent and excluded.</summary>
    public List<string> GetOpenTabKeys() =>
        Tabs.Select(TabKey).Where(k => k != null).Select(k => k!).ToList();

    public string? GetSelectedTabKey() => TabKey(SelectedTab);

    private static string? TabKey(TabViewModelBase? tab) => tab switch
    {
        null or MyTicketsViewModel => null,
        SearchViewModel => "search",
        QueueViewModel => "queue",
        BookmarksViewModel => "bookmarks",
        SettingsViewModel => "settings",
        CreateTicketViewModel => "newticket",
        TicketTabViewModel t => $"ticket:{t.TicketId}",
        _ => null
    };

    private void OnBookmarkChanged()
    {
        if (_bookmarks != null && Tabs.Contains(_bookmarks))
            _ = _bookmarks.RefreshAsync();
    }

    /// <summary>Removes a closable tab; selects the last-viewed tab when the closed one was selected.</summary>
    private void CloseTab(TabViewModelBase tab)
    {
        var wasSelected = SelectedTab == tab;
        _lastViewed.Remove(tab);
        // Compute the fallback BEFORE removing: the TabControl pushes its own
        // neighbour-selection through the binding as soon as the tab disappears.
        var fallback = wasSelected
            ? _lastViewed.LastOrDefault() ?? _myTickets ?? Tabs.LastOrDefault(t => t != tab)
            : null;
        _closing = true;
        Tabs.Remove(tab);
        _closing = false;
        if (tab is QueueViewModel q) q.StopAutoRefresh();
        if (wasSelected) SelectedTab = fallback;
    }

    // Recency stack of viewed tabs (most recent last); kept in sync with Tabs by CloseTab.
    private readonly List<TabViewModelBase> _lastViewed = new();
    private bool _closing;

    partial void OnSelectedTabChanged(TabViewModelBase? value)
    {
        if (_closing || value == null) return;
        _lastViewed.Remove(value);
        _lastViewed.Add(value);
    }

    [RelayCommand]
    private void OpenNewTicket()
    {
        CreateTicketViewModel? tab = null;
        tab = new CreateTicketViewModel(_session, ticketId =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (tab != null) Tabs.Remove(tab);
                OpenTicket(ticketId);
            });
        });
        tab.CloseRequested += (_, _) => CloseTab(tab);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    private void OpenTicketByNumber(string? idText)
    {
        if (int.TryParse(idText, out var id) && id > 0)
            OpenTicket(id);
    }

    [RelayCommand]
    private void ShowPage(string? name)
    {
        TabViewModelBase? target = name switch
        {
            "mine" => _myTickets,
            "search" => _search ??= new SearchViewModel(_session, _settings, OpenTicket),
            "queue" => _queue ??= new QueueViewModel(_session, _settings, OpenTicket),
            "settings" => _settingsPage ??= new SettingsViewModel(_settings, _session, _updates, SignOut, _gridLayout),
            "bookmarks" => _bookmarks ??= new BookmarksViewModel(_session, _settings, OpenTicket),
            _ => null
        };
        if (target == null) return;
        if (!Tabs.Contains(target))
        {
            target.CloseRequested += (_, _) => CloseTab(target);
            // Pages stay pinned to the left: insert after the last page tab,
            // before any ticket / new-ticket tabs.
            Tabs.Insert(PageInsertIndex, target);
            if (target is QueueViewModel q) q.StartAutoRefresh();
            if (target is BookmarksViewModel b) _ = b.RefreshAsync();
        }
        SelectedTab = target;
        // The Updates section refreshes itself whenever the Settings page is opened
        // (the command no-ops while a check is already running).
        if (target is SettingsViewModel s) s.CheckForUpdatesCommand.Execute(null);
    }

    /// <summary>Index after the last page tab; ticket tabs always open at or after this index.</summary>
    private int PageInsertIndex =>
        Tabs.Count(t => t is not TicketTabViewModel and not CreateTicketViewModel);

    [RelayCommand]
    private void SignOut()
    {
        _polling.Stop();
        // Demo mode must never clear the real saved key.
        if (!WhdSessionContext.IsDemoMode)
        {
            _settings.ClearApiKey();
            _settings.Save();
        }
        _session.SignOut();

        var login = new LoginWindow
        {
            DataContext = App.Services.GetRequiredService<LoginViewModel>()
        };
        Application.Current.MainWindow = login;
        login.Show();
        foreach (Window w in Application.Current.Windows.OfType<MainWindow>().ToList())
            w.Close();
    }
}
